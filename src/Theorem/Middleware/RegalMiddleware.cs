using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    /// The Regal middleware will post a summary of upcoming Regal movies
    /// every week to a given channel.
    /// </summary>
    public class RegalMiddleware : IMiddleware
    {
        private ILogger<RegalMiddleware> _logger;

        private ConfigurationSection _configuration;

        private IEnumerable<IChatServiceConnection> _chatServiceConnections;

        private Dictionary<IChatServiceConnection, string> _chatServicePostChannelId = new();

        private DayOfWeek _postDayOfWeek = DayOfWeek.Monday;

        private TimeSpan _postTimeOfDay = new(8, 0, 0);

        private string[] _locationCodes;

        private string _postPrefix = "🌲 Hello volunteer friends! The following events are " + 
            "happening soon in our area. If you're interested, please sign up and " + 
            "ask others to join!";

        private string _noEventsMessage = "📃 I couldn't find any volunteering events this week. " +
            "Check local parks pages and other resources and share here if you find " +
            "opportunities!";

        private Timer _postTimer;

        private HttpClient _httpClient = new();

        public RegalMiddleware(
            ILogger<RegalMiddleware> logger,
            ConfigurationSection configuration,
            IEnumerable<IChatServiceConnection> chatServiceConnections)
        {
            _logger = logger;
            _configuration = configuration;
            _chatServiceConnections = chatServiceConnections;

            parseConfiguration();
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
            if (_configuration.GetSection("LocationCodes").Exists())
            {
                _locationCodes = _configuration.GetSection("LocationCodes").Get<string[]>();
            }
            else
            {
                successfulParse = false;
            }
            if (!successfulParse)
            {
                _logger.LogError("Could not parse configuration values.");
            }
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
            _logger.LogInformation($"Scheduling next Regal post for {nextInviteTime}");
            var nextInviteTimeMs = (int)nextInviteTime
                .Subtract(DateTimeOffset.Now).TotalMilliseconds;
            _postTimer = new Timer(OnPostTimer, null, nextInviteTimeMs, Timeout.Infinite);
        }

        private void OnPostTimer(object state)
        {
            _postTimer.Dispose();
            PostMoviesAsync();
            schedulePostTimer();
        }

        private async void PostMoviesAsync()
        {
            try
            {
                _logger.LogInformation($"Fetching movies...");
                var movieSchedule = await GetMoviesForLocationsAsync(_locationCodes,
                    DateOnly.FromDateTime(DateTime.Now),
                    DateOnly.FromDateTime(DateTime.Now.AddDays(7)));

                string mainMsg = "📽️ Upcoming theater showings for the week of " +
                    $"{DateTime.Now:M/d}:\n\n";
                string detailedMsg = "📅 Detailed schedule:\n\n";
                foreach (var film in movieSchedule.Films.Values.OrderBy(f => f.Title))
                {
                    string titleHeader = $"🎞️ {film.Title}\n";
                    mainMsg += titleHeader;
                    detailedMsg += titleHeader;
                    var showings = movieSchedule.ShowingsByFilmId[film.Id];
                    var showingsByCinema = showings.GroupBy(s => s.CinemaId)
                        .OrderBy(g => Array.IndexOf(_locationCodes, g.Key));
                    foreach (var showingGroup in showingsByCinema)
                    {
                        var cinema = movieSchedule.Cinemas[showingGroup.Key];
                        string daysOfWeek = string.Join(" ", showingGroup
                            .OrderBy(g => g.ShowingTime).Select(g => g.ShowingTime.DayOfWeek)
                            .Distinct().Select(d => DayOfWeekAbbreviation(d)));
                        mainMsg += $"\t📍 {cinema.DisplayName}: {daysOfWeek}";
                        detailedMsg += $"\t📍 {cinema.DisplayName}:\n";
                        var perDayGroups = showingGroup.GroupBy(s => s.ShowingTime.Date);
                        foreach (var perDayGroup in perDayGroups)
                        {
                            detailedMsg += $"\t{perDayGroup.Key:ddd M/d}: ";
                            bool firstShowing = true;
                            foreach (var showing in perDayGroup)
                            {
                                if (!firstShowing)
                                {
                                    detailedMsg += ", ";
                                }
                                detailedMsg += $"{showing.ShowingTime:hh:mmtt}";
                                firstShowing = false;
                            }
                            detailedMsg += "\n";
                        }
                        mainMsg += "\n";
                        detailedMsg += "\n";
                    }
                    mainMsg += "\n";
                }
                mainMsg += "For a detailed schedule, see the thread.";

                foreach (var connection in _chatServicePostChannelId)
                {
                    _logger.LogInformation(
                        $"Posting movies message to {connection.Value}...");
                    var threadId = await connection.Key.SendMessageToChannelIdAsync(
                        connection.Value,
                        new ChatMessageModel()
                        {
                            Body = mainMsg,
                        });
                    await connection.Key.SendMessageToChannelIdAsync(
                        connection.Value,
                        new ChatMessageModel()
                        {
                            Body = detailedMsg,
                            ThreadingId = threadId,
                        });
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"Could not post upcoming movies: {e.Message}");
            }
        }

        private async Task<FilmScheduleModel> GetMoviesForLocationsAsync(
            IEnumerable<string> locationCodes, DateOnly startDate, DateOnly endDate)
        {
            var returnValue = new FilmScheduleModel();
            // First we need to translate location codes to names.
            var endDateString = endDate.ToString("yyyy-MM-dd");
            _logger.LogInformation("Fetching cinema list from Regal API...");
            var locations = new Dictionary<string, string>();
            {
                var request = new HttpRequestMessage()
                    {
                        RequestUri = new Uri("https://www.regmovies.com/us/data-api-service/v1" + 
                            $"/quickbook/10110/cinemas/with-event/until/{endDateString}"),
                        Method = HttpMethod.Get,
                    };
                var result = await _httpClient.SendAsync(request);
                if (!result.IsSuccessStatusCode)
                {
                    throw new ApplicationException("Error fetching cinema location list from " +
                        $"Regal: HTTP {result.StatusCode}");
                }
                var response = await JsonSerializer.DeserializeAsync<RegalCinemasResponse>(
                    await result.Content.ReadAsStreamAsync());
                locations = response.Body.Cinemas.ToDictionary(m => m.Id, m => m.DisplayName);
                foreach (var locationCode in locationCodes)
                {
                    returnValue.Cinemas[locationCode] = new FilmScheduleModel.CinemaModel()
                    {
                        Id = locationCode,
                        DisplayName = locations[locationCode],
                    };
                }
            }
            
            // Now, we fetch movies + events for each location on each day
            _logger.LogInformation("Fetching showings from Regal API...");
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var dateString = date.ToString("yyyy-MM-dd");
                foreach(var locationCode in locationCodes)
                {
                    var request = new HttpRequestMessage()
                        {
                            RequestUri = new Uri("https://www.regmovies.com/us/data-api-service" + 
                                "/v1/quickbook/10110/film-events/in-cinema" + 
                                $"/{locationCode}/at-date/{dateString}"),
                            Method = HttpMethod.Get,
                        };
                    var result = await _httpClient.SendAsync(request);
                    if (!result.IsSuccessStatusCode)
                    {
                        throw new ApplicationException("Error fetching movie events from " +
                            $"Regal: HTTP {result.StatusCode}");
                    }
                    var responseStr = await result.Content.ReadAsStringAsync();
                    var response = await JsonSerializer.DeserializeAsync<RegalFilmEventsResponse>(
                        await result.Content.ReadAsStreamAsync());
                    var responseFilms = response.Body.Films.ToDictionary(f => f.Id, f => f);
                    foreach (var showing in response.Body.Events)
                    {
                        // Add missing films
                        if (!returnValue.Films.ContainsKey(showing.FilmId))
                        {
                            if (!responseFilms.ContainsKey(showing.FilmId))
                            {
                                continue;
                            }
                            var responseFilm = responseFilms[showing.FilmId];
                            returnValue.Films[showing.FilmId] = new FilmScheduleModel.FilmModel()
                            {
                                Id = responseFilm.Id,
                                Title = responseFilm.Title,
                                Duration = new TimeSpan(0, responseFilm.DurationMinutes, 0),
                                PosterLink = responseFilm.PosterLink,
                                VideoLink = responseFilm.VideoLink,
                            };
                        }

                        // Add showing
                        if (!returnValue.ShowingsByFilmId.ContainsKey(showing.FilmId))
                        {
                            returnValue.ShowingsByFilmId[showing.FilmId] = new();
                        }
                        returnValue.ShowingsByFilmId[showing.FilmId].Add(
                            new FilmScheduleModel.ShowingModel()
                            {
                                FilmId = showing.FilmId,
                                CinemaId = locationCode,
                                BookingLink = showing.BookingLink,
                                ShowingTime = showing.ShowTime,
                            });
                    }
                }
            }
            return returnValue;
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

        private static string DayOfWeekAbbreviation(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Sunday => "U",
                DayOfWeek.Monday => "M",
                DayOfWeek.Tuesday => "T",
                DayOfWeek.Wednesday => "W",
                DayOfWeek.Thursday => "R",
                DayOfWeek.Friday => "F",
                DayOfWeek.Saturday => "S",
                _ => "",
            };
        }

        private class FilmScheduleModel
        {
            public Dictionary<string, FilmModel> Films = new();
            public Dictionary<string, CinemaModel> Cinemas = new();
            public Dictionary<string, List<ShowingModel>> ShowingsByFilmId = new();

            public class FilmModel
            {
                public string Id { get; set; }
                public string Title { get; set; }
                public TimeSpan Duration { get; set; }
                public Uri PosterLink { get; set; }
                public Uri VideoLink { get; set; }
            }

            public class CinemaModel
            {
                public string Id { get; set; }
                public string DisplayName { get; set; }
            }

            public class ShowingModel
            {
                public string FilmId { get; set; }
                public string CinemaId { get; set; }
                public DateTime ShowingTime { get; set; }
                public Uri BookingLink { get; set; }
            }
        }

        private class RegalCinemasResponse
        {
            [JsonPropertyName("body")]
            public ResponseBody Body { get; set; }

            public class ResponseBody
            {
                [JsonPropertyName("cinemas")]
                public IEnumerable<Cinema> Cinemas { get; set; }

            }

            public class Cinema
            {
                [JsonPropertyName("id")]
                public string Id { get; set; }

                [JsonPropertyName("displayName")]
                public string DisplayName { get; set; }
            }
        }

        private class RegalFilmEventsResponse
        {
            [JsonPropertyName("body")]
            public ResponseBody Body { get; set; }
            
            public class ResponseBody
            {
                [JsonPropertyName("films")]
                public IEnumerable<Film> Films { get; set; }

                [JsonPropertyName("events")]
                public IEnumerable<Event> Events { get; set; }
            }

            public class Film
            {
                [JsonPropertyName("id")]
                public string Id { get; set; }

                [JsonPropertyName("name")]
                public string Title { get; set; }

                [JsonPropertyName("length")]
                public int DurationMinutes { get; set; }

                [JsonPropertyName("posterLink")]
                public Uri PosterLink { get; set; }

                [JsonPropertyName("videoLink")]
                public Uri VideoLink { get; set; }
            }

            public class Event
            {
                [JsonPropertyName("id")]
                public string Id { get; set; }

                [JsonPropertyName("filmId")]
                public string FilmId { get; set; }

                [JsonPropertyName("eventDateTime")]
                public DateTime ShowTime { get; set; }

                [JsonPropertyName("bookingLink")]
                public Uri BookingLink { get; set; }

                [JsonPropertyName("auditorium")]
                public string Auditorium { get; set; }
            }
        }
    }
}