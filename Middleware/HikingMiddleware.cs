using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Theorem.Models;
using Theorem.Models.Events;
using Theorem.Models.Slack;
using Theorem.Models.Slack.Events;
using Theorem.Providers;

namespace Theorem.Middleware
{
    /// <summary>
    /// The Hiking middleware allows Theorem to fetch a hike from WTA,
    /// as well as schedule weekly hikes.
    /// </summary>
    public class HikingMiddleware : Middleware
    {
        private const string _baseUrl = "http://www.wta.org";
        private const string _apiPath = "/go-hiking/map/@@trailhead-search/getHikes?jsonp_callback=WtaTrailheadSearch.setHikes";
        private const string _detailsUrl = "http://www.wta.org/go-hiking/hikes/{0}";
        private const string _thumbUrl = "http://www.wta.org/go-hiking/hikes/{0}/photo1_grid-thumb";

        /// <summary>
        /// Pattern used to match messages.
        /// </summary>
        private const string _messagePattern = @".*<@{0}>.*hike.*";

        /// <summary>
        /// Regex used to match messages.
        /// </summary>
        private Regex _messageRegex { get; set; }

        /// <summary>
        /// The Slack ID of the channel to post hike updates
        /// </summary>
        private string _hikingChannelSlackId { get; set; }

        /// <summary>
        /// A timer to trigger the hike invite
        /// </summary>
        private Timer _inviteTimer;

        #region Configuration Values
        private string _hikingChannelName = "hiking";
        private double _originLatitude = 47.636524;
        private double _originLongitude = -122.129606;
        private double _maxDistanceToHikeMi = 30;
        private double _minRating = 3;
        private double _minLengthMi = 4;
        private double _maxLengthMi = 10;
        private double _maxElevationGainFt = 2500;
        private bool _scheduleHikes = false;
        private DayOfWeek _inviteDayOfWeek;
        private TimeSpan _inviteTimeOfDay;
        private DayOfWeek _hikeDayOfWeek;
        private TimeSpan _hikeTimeOfDay;
        private string _meetingLocation;
        #endregion

        /// <summary>
        /// Class to serialize WTA data into
        /// </summary>
        private class Hike
        {
            //"rating": "0.0", "length": "3.5", "kml": "", "features": "rfkd", "name": "May Creek Trail", "lat": "47.5226", "lng": "-122.1655", "elevGain": null, "id": "may-creek-trail", "elevMax": null
            [JsonProperty("id")]
            public string Id { get; set; }
            [JsonProperty("name")]
            public string Name { get; set; }
            [JsonProperty("rating")]
            public double Rating { get; set; }
            [JsonProperty("length")]
            public double? LengthMiles { get; set; }
            [JsonProperty("lat")]
            public double Latitude { get; set; }
            [JsonProperty("lng")]
            public double Longitude { get; set; }
            [JsonProperty("elevGain")]
            public double? ElevationGainFt { get; set; }
            [JsonProperty("elevMax")]
            public double? ElevationMaxFt { get; set; }
        }

        public HikingMiddleware(SlackProvider slackProvider, Func<ApplicationDbContext> dbContext, IConfigurationRoot configuration)
            : base(slackProvider, dbContext, configuration)
        {
            _slackProvider.Connected += slackConnected;

            // Load configuration values
            _hikingChannelName = _configuration["Middleware:Hiking:ChannelName"];
            if (_configuration["Middleware:Hiking:OriginLatitude"] != null)
            {
                double.TryParse(_configuration["Middleware:Hiking:OriginLatitude"], out _originLatitude);
            }
            if (_configuration["Middleware:Hiking:OriginLongitude"] != null)
            {
                double.TryParse(_configuration["Middleware:Hiking:OriginLongitude"], out _originLongitude);
            }
            if (_configuration["Middleware:Hiking:MaxDistanceToHikeMi"] != null)
            {
                double.TryParse(_configuration["Middleware:Hiking:MaxDistanceToHikeMi"], out _maxDistanceToHikeMi);
            }
            if (_configuration["Middleware:Hiking:MinRating"] != null)
            {
                double.TryParse(_configuration["Middleware:Hiking:MinRating"], out _minRating);
            }
            if (_configuration["Middleware:Hiking:MinLengthMi"] != null)
            {
                double.TryParse(_configuration["Middleware:Hiking:MinLengthMi"], out _minLengthMi);
            }
            if (_configuration["Middleware:Hiking:MaxLengthMi"] != null)
            {
                double.TryParse(_configuration["Middleware:Hiking:MaxLengthMi"], out _maxLengthMi);
            }
            if (_configuration["Middleware:Hiking:MaxElevationGainFt"] != null)
            {
                double.TryParse(_configuration["Middleware:Hiking:MaxElevationGainFt"], out _maxElevationGainFt);
            }
            bool.TryParse(_configuration["Middleware:Hiking:Schedule:ScheduleHikes"], out _scheduleHikes);
            Enum.TryParse(_configuration["Middleware:Hiking:Schedule:InviteDayOfWeek"], out _inviteDayOfWeek);
            TimeSpan.TryParse(_configuration["Middleware:Hiking:Schedule:InviteTime"], out _inviteTimeOfDay);
            Enum.TryParse(_configuration["Middleware:Hiking:Schedule:HikeDayOfWeek"], out _hikeDayOfWeek);
            TimeSpan.TryParse(_configuration["Middleware:Hiking:Schedule:HikeTime"], out _hikeTimeOfDay);
            _meetingLocation = _configuration["Middleware:Hiking:Schedule:MeetingLocation"];
            if (_scheduleHikes)
            {
                if (_inviteTimeOfDay != null
                    && _hikeTimeOfDay != null
                    && _meetingLocation != null)
                {
                    ScheduleInviteTimer();
                }
                else
                {
                    Console.WriteLine("Could not schedule hikes due to invalid configuration values.");
                }
            }
        }

        private void ScheduleInviteTimer()
        {
            var nextInviteTime = NextDateTime(_inviteDayOfWeek, _inviteTimeOfDay);
            var nextInviteTimeMs = (int)nextInviteTime.Subtract(DateTimeOffset.Now).TotalMilliseconds;
            _inviteTimer = new Timer(OnInviteTimer, null, nextInviteTimeMs, Timeout.Infinite);
        }

        private void OnInviteTimer(object state)
        {
            _inviteTimer.Dispose();
            var nextHikeTime = NextDateTime(_hikeDayOfWeek, _hikeTimeOfDay);
            var nextHikeTimeString = nextHikeTime.ToString("dddd, M/dd @ h:mm tt");
            var message = $"<!channel> let's plan a hike! Meet "
                + $"at *{_meetingLocation}* on *{nextHikeTimeString}*. Hike details are below.";
            if (_hikingChannelSlackId == null)
            {
                _hikingChannelSlackId = _slackProvider.GetChannelByName(_hikingChannelName)?.SlackId;
            }
            if (_hikingChannelSlackId == null)
            {
                Console.WriteLine(String.Format("Error: Could not find hiking channel #{0}.", _hikingChannelName));
            }
            else
            {
                postNewHike(_hikingChannelSlackId, message);
            }
            ScheduleInviteTimer();
        }

        /// <summary>
        /// Given a day of week and time of day, returns the DateTimeOffset
        /// of the next time this occurs.
        /// </summary>
        private DateTimeOffset NextDateTime(DayOfWeek dayOfWeek, TimeSpan timeOfDay)
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

        private void slackConnected(object sender, EventArgs e)
        {
            // Load configuration values for channel
            var channel = _slackProvider.GetChannelByName(_hikingChannelName);
            if (channel == null)
            {
                Console.WriteLine(String.Format("Error: Could not find hiking channel #{0}.", _hikingChannelName));
                return;
            }
            _hikingChannelSlackId = channel.SlackId;

            // Compile mention regex
            if (_messageRegex == null)
            {
                _messageRegex = new Regex(String.Format(_messagePattern, _slackProvider.Self.Id), RegexOptions.IgnoreCase);
            }
        }

        public override MiddlewareResult ProcessMessage(SlackMessageEventModel message)
        {
            var match = _messageRegex.Match(message.Text);
            if (match.Success)
            {
                postNewHike(message.SlackChannelId, $"Here you go, <@{message.SlackUserId}>.");
                return MiddlewareResult.Stop;
            }
            return MiddlewareResult.Continue;
        }

        /// <summary>
        /// Posts a new hike to the given Slack channel Id with the given message.
        /// </summary>
        /// <param name="channelId">Slack channel ID</param>
        /// <param name="message">Message</param>
        private async void postNewHike(string channelId, string message)
        {
            var hikes = await getFilteredHikesAsync();
            var random = new Random();
            var selectedHike = hikes[random.Next(hikes.Count)];
            await _slackProvider.SendMessageToChannelId(
                channelId,
                message,
                new List<SlackAttachmentModel>() { formatHikeAttachment(selectedHike) }
            );
        }

        /// <summary>
        /// Formats the given hike into a Slack message attachment.
        /// </summary>
        /// <param name="hike">The hike to format</param>
        /// <returns>A Slack attachment to be included with a message</returns>
        private SlackAttachmentModel formatHikeAttachment(Hike hike)
        {
            var hikeDistanceFromCampus = calculateCoordinateDistanceMi(_originLatitude, _originLongitude, hike.Latitude, hike.Longitude);
            var hikeAttachment = new SlackAttachmentModel()
            {
                Fallback = String.Format("{0}", hike.Name),
                Color = "good",
                Title = hike.Name,
                TitleLink = String.Format(_detailsUrl, hike.Id),
                ThumbUrl = String.Format(_thumbUrl, hike.Id),
                Fields = new List<SlackAttachmentFieldModel>()
                {
                    new SlackAttachmentFieldModel()
                    {
                        Title = "Distance From Campus",
                        Value = $"{hikeDistanceFromCampus:N2} mi",
                        Short = true
                    },
                    new SlackAttachmentFieldModel()
                    {
                        Title = "Length",
                        Value = $"{hike.LengthMiles:N2} mi",
                        Short = true
                    },
                    new SlackAttachmentFieldModel()
                    {
                        Title = "Elevation Gain",
                        Value = $"{hike.ElevationGainFt:N0} ft",
                        Short = true
                    },
                    new SlackAttachmentFieldModel()
                    {
                        Title = "Max Elevation",
                        Value = $"{hike.ElevationMaxFt:N0} ft",
                        Short = true
                    },
                    new SlackAttachmentFieldModel()
                    {
                        Title = "Rating",
                        Value = $"{hike.Rating:N1}/5",
                        Short = true
                    },
                    new SlackAttachmentFieldModel()
                    {
                        Title = "Map",
                        Value = $"<http://bing.com/maps/default.aspx?rtp=pos.{_originLatitude}_{_originLongitude}~pos.{hike.Latitude}_{hike.Longitude}|View Map>",
                        Short = true
                    }
                }
            };
            return hikeAttachment;
        }

        /// <summary>
        /// Fetches a list of hikes and runs filters based on mininum rating,
        /// min/max length, max elevation gain, maximum distance from origin.
        /// </summary>
        /// <returns>Filtered list of hikes</returns>
        private async Task<List<Hike>> getFilteredHikesAsync()
        {
            var hikes = await fetchHikesAsync();
            var filteredHikes = hikes
                    .Where(h => h.Rating > _minRating)
                    .Where(h => h.LengthMiles != null && h.LengthMiles > _minLengthMi && h.LengthMiles < _maxLengthMi)
                    .Where(h => h.ElevationGainFt != null && h.ElevationGainFt < _maxElevationGainFt)
                    .Where(h => h.ElevationMaxFt != null)
                    .Where(h => calculateCoordinateDistanceMi(_originLatitude, _originLongitude, h.Latitude, h.Longitude) < _maxDistanceToHikeMi)
                    .ToList();
            return filteredHikes;
        }

        /// <summary>
        /// Fetches all hikes from WTA site.
        /// </summary>
        /// <returns>List of all hikes</returns>
        private async Task<List<Hike>> fetchHikesAsync()
        {
            using (var client = new HttpClient())
            {
                // Request hikes
                client.BaseAddress = new Uri(_baseUrl);
                var request = await client.GetAsync(_apiPath);
                var result = await request.Content.ReadAsStringAsync();
                result = result.Substring(28, result.Length - 28 - 1);
                var hikes = JsonConvert.DeserializeObject<List<Hike>>(result);
                return hikes;
            }
        }

        /// <summary>
        /// Calculates the distance between two sets of coordinates in miles.
        /// </summary>
        /// <returns>Distance between two coordinates in miles</returns>
        private double calculateCoordinateDistanceMi(double firstLatitude, double firstLongitude, double secondLatitude, double secondLongitude)
        {
            var r = 3959; // radius of earth in miles
            var thetaOne = firstLatitude * (Math.PI / 180);
            var thetaTwo = secondLatitude * (Math.PI / 180);
            var deltaTheta = (secondLatitude - firstLatitude) * (Math.PI / 180);
            var deltaLambda = (secondLongitude - firstLongitude) * (Math.PI / 180);

            var a = Math.Sin(deltaTheta / 2) * Math.Sin(deltaTheta / 2) + 
                Math.Cos(thetaOne) * Math.Cos(thetaTwo) *
                Math.Sin(deltaLambda / 2) * Math.Sin(deltaLambda / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return r * c;
        }
    }
}