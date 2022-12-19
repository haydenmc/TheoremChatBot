using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Theorem.ChatServices;
using Theorem.Models;

namespace Theorem.Middleware
{
    /// <summary>
    /// The Volunteering middleware will post a summary of upcoming volunteering events
    /// every week to a given channel.
    /// </summary>
    public class VolunteeringMiddleware : IMiddleware
    {
        private ILogger<VolunteeringMiddleware> _logger;

        private ConfigurationSection _configuration;

        private IEnumerable<IChatServiceConnection> _chatServiceConnections;

        private Dictionary<IChatServiceConnection, string> _chatServicePostChannelId
            = new Dictionary<IChatServiceConnection, string>();

        private DayOfWeek _postDayOfWeek = DayOfWeek.Monday;

        private TimeSpan _postTimeOfDay = new TimeSpan(8, 0, 0);

        private string _postPrefix = "🌲 Hello volunteer friends! The following events are " + 
            "happening soon in our area. If you're interested, please sign up and " + 
            "ask others to join!";

        private string _noEventsMessage = "📃 I couldn't find any volunteering events this week. " +
            "Check local parks pages and other resources and share here if you find " +
            "opportunities!";

        private Timer _postTimer;

        private List<IVolunteeringEventDataSource> _volunteeringEventDataSources = 
            new List<IVolunteeringEventDataSource>();

        public VolunteeringMiddleware(
            ILogger<VolunteeringMiddleware> logger,
            ConfigurationSection configuration,
            IEnumerable<IChatServiceConnection> chatServiceConnections)
        {
            _logger = logger;
            _configuration = configuration;
            _chatServiceConnections = chatServiceConnections;

            parseConfiguration();
            initializeVolunteeringEventDataSources();
            subscribeToChatServiceConnectedEvents();
            schedulePostTimer();
        }

        public MiddlewareResult ProcessMessage(ChatMessageModel message)
        {
            return MiddlewareResult.Continue;
        }

        private void parseConfiguration()
        {
            bool successfulParse = true;
            successfulParse &= Enum.TryParse(_configuration["PostDayOfWeek"],
                out _postDayOfWeek);
            successfulParse &= TimeSpan.TryParse(_configuration["PostTime"],
                out _postTimeOfDay);
            if (!successfulParse)
            {
                _logger.LogError("Could not parse configuration values.");
            }
        }

        private void initializeVolunteeringEventDataSources()
        {
            _volunteeringEventDataSources.Add(new BellevueParksVolunteeringEventDataSource());
            _volunteeringEventDataSources.Add(new SeattleParksVolunteeringEventDataSource());
        }

        private void subscribeToChatServiceConnectedEvents()
        {
            foreach (var chatService in _chatServiceConnections)
            {
                chatService.Connected += onChatServiceConnected;
            }
        }

        private async void onChatServiceConnected(object sender, EventArgs e)
        {
            _logger.LogInformation("onChatServiceConnected");
            var connection = sender as IChatServiceConnection;
            var matchingService = _configuration
                .GetSection("PostChannels")
                .GetChildren()
                .SingleOrDefault(s => s.GetValue<string>("ChatServiceName") == connection.Name);
            if (matchingService != null)
            {
                var channelName = matchingService.GetValue<string>("ChannelName");
                var channelId = await connection.GetChannelIdFromChannelNameAsync(channelName);
                _chatServicePostChannelId[connection] = channelId;
                _logger.LogInformation(
                    "Chat service connection {name} connected, using channel {channel}:{id}.",
                    connection.Name,
                    channelName,
                    channelId);
            }
        }

        private void schedulePostTimer()
        {
            var nextInviteTime = NextDateTime(_postDayOfWeek, _postTimeOfDay);
            _logger.LogInformation($"Scheduling next volunteering post for {nextInviteTime}");
            var nextInviteTimeMs = (int)nextInviteTime
                .Subtract(DateTimeOffset.Now).TotalMilliseconds;
            _postTimer = new Timer(OnPostTimer, null, nextInviteTimeMs, Timeout.Infinite);
        }

        private void OnPostTimer(object state)
        {
            _postTimer.Dispose();
            PostVolunteeringEventsAsync();
            schedulePostTimer();
        }

        private async void PostVolunteeringEventsAsync()
        {
            try
            {
                _logger.LogInformation($"Fetching volunteering events...");
                var events = await GetVolunteeringEventsAsync(
                    DateTimeOffset.Now, DateTimeOffset.Now.AddDays(7));
                var message = $"{_postPrefix}";
                foreach (var theEvent in events)
                {
                    message += $"\n\n📍 {theEvent.StartTime.ToString("ddd M/d h:mmtt")} " + 
                        $"in {theEvent.Location}: {theEvent.Title}\n" + 
                        $"{theEvent.Url}";
                }
                if (events.Count == 0)
                {
                    message = _noEventsMessage;
                }
                foreach (var connection in _chatServicePostChannelId)
                {
                    _logger.LogInformation(
                        $"Posting volunteering message to {connection.Value}...");
                    await connection.Key.SendMessageToChannelIdAsync(
                        connection.Value,
                        new ChatMessageModel()
                        {
                            Body = message,
                        });
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"Could not post volunteering events: {e.Message}");
            }
        }

        private async Task<IList<VolunteeringEvent>> GetVolunteeringEventsAsync(
            DateTimeOffset startTime, DateTimeOffset endTime)
        {
            var events = new List<VolunteeringEvent>();
            foreach (var dataSource in _volunteeringEventDataSources)
            {
                try
                {
                    _logger.LogInformation(
                        $"Fetching volunteering events from {dataSource.GetType().Name}...");
                    var dataSourceEvents = await dataSource.GetVolunteeringEventsAsync(
                        startTime, endTime);
                    events.AddRange(dataSourceEvents);
                }
                catch (Exception e)
                {
                    _logger.LogError("Could not retrieve events from source " + 
                        $"'{dataSource.GetType().FullName}': {e.Message}");
                }
            }
            return events.OrderBy(e => e.StartTime).ToList();
        }

        /// <summary>
        /// Given a day of week and time of day, returns the DateTimeOffset
        /// of the next time this occurs.
        /// </summary>
        private static DateTimeOffset NextDateTime(DayOfWeek dayOfWeek, TimeSpan timeOfDay)
        {
            var localOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
            var now = DateTimeOffset.Now;
            var today = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, 0, localOffset);
            var currentDow = (int)now.DayOfWeek;
            int dayCount;
            if (currentDow < (int)dayOfWeek)
            {
                dayCount = (int)dayOfWeek - currentDow;
            }
            else if (currentDow == (int)dayOfWeek)
            {
                var todayTime = now.Subtract(today);
                if (todayTime < timeOfDay)
                {
                    dayCount = 0;
                }
                else
                {
                    dayCount = 7;
                }
            }
            else
            {
                dayCount = 7 - (currentDow - (int)dayOfWeek);
            }
            var nextTime = today.AddDays(dayCount).Add(timeOfDay);
            return nextTime;
        }

        private class VolunteeringEvent
        {
            public string Url { get; set; }
            public DateTimeOffset StartTime { get; set; }
            public string Title { get; set; }
            public string Location { get; set; }
        }

        private interface IVolunteeringEventDataSource
        {
            public Task<IList<VolunteeringEvent>> GetVolunteeringEventsAsync(
                DateTimeOffset startTime, DateTimeOffset endTime);
        }

        private class BellevueParksVolunteeringEventDataSource : IVolunteeringEventDataSource
        {
            private HttpClient _httpClient = new HttpClient();

            private readonly TimeZoneInfo _timeZone =
                TimeZoneInfo.GetSystemTimeZones().Any(t => (t.Id == "Pacific Standard Time"))
                    ? TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time")
                    : TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

            public async Task<IList<VolunteeringEvent>> GetVolunteeringEventsAsync(
                DateTimeOffset startTime, DateTimeOffset endTime)
            {
                var events = new List<VolunteeringEvent>();
                var startDateStr = startTime.ToString("yyyy-M-d");
                // Add an extra day to the end time that we pass, since the time is set to '0:0:0'
                var endDateStr = endTime.AddDays(1).ToString("yyyy-M-d"); 
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri("https://bellevuewa-apps.my.salesforce-sites.com" +
                        "/volunteers/apexremote"),
                    Method = HttpMethod.Post,
                    Content = new StringContent(@"{""action"":""GW_Volunteers.VOL_CTRL_JobCalendar"",""method"":""getListShiftsWeb2"",""data"":[""*"",""*"",""" + startDateStr + @" 0:0:0"",""" + endDateStr + @" 0:0:0"",true,false],""type"":""rpc"",""tid"":2,""ctx"":{""csrf"":""VmpFPSxNakF5TWkweE1pMHlNVlF3TXpvMU5Ub3hPQzQxTUROYSxiT0lCLWczcU5TN19NUEc0eXpSNGVRLE1XWXhPVFps"",""vid"":""0665e000001zEyP"",""ns"":""GW_Volunteers"",""ver"":40,""authorization"":""eyJub25jZSI6IkZneWFUbDZuX2RsM0swa3QtQ0d2S1lFbzZQVEoxUHc1QWwySVlFTDkzOWdcdTAwM2QiLCJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiIsImtpZCI6IntcInRcIjpcIjAwRDVlMDAwMDAwSnpIUlwiLFwidlwiOlwiMDJHNWUwMDAwMDBZRGFXXCIsXCJhXCI6XCJ2ZnJlbW90aW5nc2lnbmluZ2tleVwiLFwidVwiOlwiMDA1NWUwMDAwMDBQQzhWXCJ9IiwiY3JpdCI6WyJpYXQiXSwiaWF0IjoxNjcxMzM1NzE4NTA0LCJleHAiOjB9.Q2pSSFYxOVdiMngxYm5SbFpYSnpMbFpQVEY5RFZGSk1YMHB2WWtOaGJHVnVaR0Z5TG1kbGRFeHBjM1JUYUdsbWRITlhaV0l5.CuqTmjPjK3360H8kBU3K1jdYMmdE9ZbgiFxaPPv2KfE=""}}", MediaTypeHeaderValue.Parse("application/json")),
                };
                request.Headers.Referrer = new Uri(
                    "https://bellevuewa-apps.my.salesforce-sites.com/volunteers/" + 
                    "GW_Volunteers__JobCalendar");
                var result = await _httpClient.SendAsync(request);
                if (!result.IsSuccessStatusCode)
                {
                    throw new ApplicationException("Error fetching volunteering events from" + 
                        $"Bellevue Parks community calendar: HTTP {result.StatusCode}");
                }
                using (var json = await JsonDocument.ParseAsync(
                    await result.Content.ReadAsStreamAsync()))
                {
                    foreach(var rootArrayObj in json.RootElement.EnumerateArray())
                    {
                        // Irritatingly, some recurring jobs are referred to by job ID instead of
                        // name later on. So we just cache those.
                        Dictionary<string, string> jobIdToName = new Dictionary<string, string>();
                        var resultArray = rootArrayObj.GetProperty("result");
                        // For some strange reason, the response payload *sometimes* buries
                        // certain properties under another property labeled "v".
                        // I have no idea why. But we have to find and chase them.
                        if (resultArray.ValueKind != JsonValueKind.Array)
                        {
                            resultArray = resultArray.GetProperty("v");
                        }
                        foreach(var resultObj in resultArray.EnumerateArray())
                        {
                            var realResultObj = resultObj;
                            JsonElement vResultObj;
                            if (realResultObj.TryGetProperty("v", out vResultObj))
                            {
                                realResultObj = vResultObj;
                            }
                            var shiftId = realResultObj.GetProperty("Id").GetString();
                            var jobId = realResultObj.GetProperty("GW_Volunteers__Volunteer_Job__c")
                                .GetString();
                            var url = "https://bellevuewa-apps.my.salesforce-sites.com/volunteers" +
                                "/GW_Volunteers__VolunteersJobListingFS?Calendar=1" +
                                $"&volunteerShiftId={shiftId}&jobId={jobId}";
                            string title = "";
                            if (jobIdToName.ContainsKey(jobId))
                            {
                                title = jobIdToName[jobId];
                            }
                            else
                            {
                                var jobObj = realResultObj
                                    .GetProperty("GW_Volunteers__Volunteer_Job__r");
                                JsonElement vJobObj = jobObj;
                                if (jobObj.TryGetProperty("v", out vJobObj))
                                {
                                    jobObj = vJobObj;
                                }
                                title = jobObj.GetProperty("Name").GetString();
                                jobIdToName[jobId] = title;
                            }

                            // Bellevue has some events that are "naturalists only" - skip these.
                            if (title.Contains("Naturalist Volunteers ONLY"))
                            {
                                continue;
                            }

                            var startTimeEpoch = realResultObj
                                .GetProperty("GW_Volunteers__Start_Date_Time__c").GetInt64();
                            var parsedStartTime = DateTimeOffset.FromUnixTimeMilliseconds(
                                startTimeEpoch);
                            var offsetStartTime = new DateTimeOffset(parsedStartTime.Year,
                                parsedStartTime.Month, parsedStartTime.Day, parsedStartTime.Hour,
                                parsedStartTime.Minute, parsedStartTime.Second,
                                _timeZone.BaseUtcOffset);
                            var hours = realResultObj.GetProperty("GW_Volunteers__Duration__c")
                                .GetDouble();
                            var volunteersNeeded = realResultObj
                                .GetProperty("GW_Volunteers__Number_of_Volunteers_Still_Needed__c")
                                .GetInt32();
                            events.Add(new VolunteeringEvent()
                            {
                                Url = url,
                                StartTime = offsetStartTime,
                                // Duration = new TimeSpan((int)hours, (int)((hours % 1) * 60),
                                //     (int)((((hours % 1) * 60) % 1) * 60)),
                                Title = title,
                                Location = "Bellevue",
                                // SpotsAvailable = volunteersNeeded,
                            });
                        }
                    }
                }

                return events;
            }
        }

        private class SeattleParksVolunteeringEventDataSource : IVolunteeringEventDataSource
        {
            private HttpClient _httpClient = new HttpClient();
            private readonly TimeZoneInfo _timeZone =
                TimeZoneInfo.GetSystemTimeZones().Any(t => (t.Id == "Pacific Standard Time"))
                    ? TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time")
                    : TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

            public async Task<IList<VolunteeringEvent>> GetVolunteeringEventsAsync(
                DateTimeOffset startTime, DateTimeOffset endTime)
            {
                var events = new List<VolunteeringEvent>();
                var result = await _httpClient.GetAsync(new Uri(
                    "https://www.trumba.com/calendars/volunteer-1.rss"));
                if (!result.IsSuccessStatusCode)
                {
                    throw new ApplicationException("Could not retrieve events from City of " + 
                        $"Seattle calendar feed: HTTP {result.StatusCode}");
                }

                var xml = new XmlDocument();
                xml.Load(await result.Content.ReadAsStreamAsync());
                var items = xml.SelectNodes("//channel/item");
                foreach (XmlNode item in items)
                {
                    var itemTime = DateTimeOffset.Parse(item.SelectSingleNode("pubDate").InnerText)
                        .ToOffset(_timeZone.BaseUtcOffset);
                    if ((itemTime < startTime) || (itemTime > endTime))
                    {
                        continue;
                    }
                    var title = item.SelectSingleNode("title").InnerText;
                    var url = item.SelectSingleNode("*[name()='x-trumba:weblink']").InnerText;

                    events.Add(new VolunteeringEvent()
                    {
                        Url = url,
                        StartTime = itemTime,
                        Title = title,
                        Location = "Seattle",
                    });
                }

                return events;
            }
        }
    }
}