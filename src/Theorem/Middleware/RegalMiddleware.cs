using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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
        private readonly ILogger<RegalMiddleware> _logger;

        private ConfigurationSection _configuration;

        private IEnumerable<IChatServiceConnection> _chatServiceConnections;

        private Dictionary<IChatServiceConnection, string> _chatServicePostChannelId = new();

        private DayOfWeek _postDayOfWeek = DayOfWeek.Monday;

        private TimeSpan _postTimeOfDay = new(8, 0, 0);

        private string[] _locationCodes;

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
                var fromDate = DateOnly.FromDateTime(DateTime.Now);
                var toDate = DateOnly.FromDateTime(DateTime.Now.AddDays(7));
                var movieSchedule = await GetMoviesForLocationsAsync(_locationCodes,
                    fromDate, toDate);

                // Post a summary message first, then use the thread to post detailed schedules
                var films = movieSchedule.Films.Values
                    .Where(f => movieSchedule.ShowingsByFilmId.ContainsKey(f.Id))
                    .OrderBy(f => f.Title).ToList();
                _logger.LogInformation($"Posting summary for {films.Count} films...");
                string summaryMsg = $"📽️ Showings {fromDate:M/d} – {toDate:M/d}:\n\n";
                string summaryMsgFormatted = $"<h1>📽️ Showings " + 
                    $"{fromDate:M/d} – {toDate:M/d}</h1>\n<p>\n";
                foreach (var film in films)
                {
                    summaryMsg += $"🎞️ {film.Title} ({film.Duration:%h}h{film.Duration:%m}m)\n";
                    if (film.VideoLink != null)
                    {
                        summaryMsgFormatted += $"🎞️ <a href=\"{film.VideoLink}\">" +
                            $"{film.Title}</a> ({film.Duration:%h}h{film.Duration:%m}m)<br />\n";
                    }
                    else
                    {
                        summaryMsgFormatted += $"🎞️ {film.Title} " + 
                            $"({film.Duration:%h}h{film.Duration:%m}m)<br />\n";
                    }
                }
                summaryMsgFormatted += "</p>";

                foreach (var connection in _chatServicePostChannelId)
                {
                    _logger.LogInformation(
                        $"Posting movies message to {connection.Value}...");
                    var threadId = await connection.Key.SendMessageToChannelIdAsync(
                        connection.Value,
                        new ChatMessageModel()
                        {
                            Body = summaryMsg,
                            FormattedBody = new(){ {"html", summaryMsgFormatted} }
                        });

                    // Post cinema schedules for each film in thread
                    foreach (var film in films)
                    {
                        string filmMsg = "", filmMsgFormatted = "";
                        var showings = movieSchedule.ShowingsByFilmId[film.Id];
                        var showingsByCinema = showings.GroupBy(s => s.CinemaId)
                            .OrderBy(g => Array.IndexOf(_locationCodes, g.Key));
                        filmMsg += $"{film.Title} ({film.Duration:%h}h{film.Duration:%m}m)\n";
                        if (film.VideoLink != null)
                        {
                            filmMsgFormatted +=
                                $"<h2><a href=\"{film.VideoLink}\">{film.Title}</a> " + 
                                $"({film.Duration:%h}h{film.Duration:%m}m)</h2>\n";
                        }
                        else
                        {
                            filmMsgFormatted += $"<h2>{film.Title} " + 
                                $"({film.Duration:%h}h{film.Duration:%m}m)</h2>\n";
                        }
                        filmMsgFormatted += "<p>";
                        foreach (var showingGroup in showingsByCinema)
                        {
                            var cinema = movieSchedule.Cinemas[showingGroup.Key];
                            filmMsg += $"📍 {cinema.DisplayName}\n";
                            filmMsgFormatted += $"<b>📍 {cinema.DisplayName}</b><br />\n";
                            var perDayGroups = showingGroup.GroupBy(s => s.ShowingTime.Date);
                            foreach (var perDayGroup in perDayGroups)
                            {
                                filmMsg += $"\t{perDayGroup.Key:ddd M/d}: ";
                                filmMsgFormatted += $"<b>{perDayGroup.Key:ddd M/d}: </b>";
                                bool firstShowing = true;
                                foreach (var showing in perDayGroup)
                                {
                                    if (!firstShowing)
                                    {
                                        filmMsg += ", ";
                                        filmMsgFormatted += ", ";
                                    }
                                    filmMsg += $"{showing.ShowingTime:hh:mmtt}";
                                    filmMsgFormatted += $"<a href=\"{showing.BookingLink}\">" + 
                                        $"{showing.ShowingTime:hh:mmtt}</a>";
                                    firstShowing = false;
                                }
                                filmMsg += "\n";
                                filmMsgFormatted += "<br />\n";
                            }
                            filmMsg += "\n";
                            filmMsgFormatted += "<br />\n";
                        }
                        filmMsgFormatted += "</p>";
                        await connection.Key.SendMessageToChannelIdAsync(
                            connection.Value,
                            new ChatMessageModel()
                            {
                                Body = filmMsg,
                                ThreadingId = threadId,
                                FormattedBody = new(){ {"html", filmMsgFormatted} }
                            });
                    }
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

            // First, we need a "build id", since it's needed in the Regal API URI.
            // We can extract it from any Regal front-end page.
            string buildId;
            {
                var request = new HttpRequestMessage()
                    {
                        RequestUri = new Uri("https://www.regmovies.com/"),
                        Method = HttpMethod.Get,
                    };
                var result = await _httpClient.SendAsync(request);
                if (!result.IsSuccessStatusCode)
                {
                    throw new ApplicationException("Could not retrieve Regal homepage to read " + 
                        "build id.");
                }
                var resultContent = await result.Content.ReadAsStringAsync();
                var buildIdMatch = Regex.Match(resultContent,
                    "\"buildId\"[\\s]*:[\\s]*\"([a-zA-Z0-9-_]+)\"");
                if (!buildIdMatch.Success || (buildIdMatch.Groups.Count < 1))
                {
                    throw new ApplicationException("Could not find build id in Regal homepage.");
                }
                buildId = buildIdMatch.Groups[1].Value;
            }

            // Pull the list of Regal theaters
            {
                var request = new HttpRequestMessage()
                    {
                        RequestUri = new Uri(
                            $"https://www.regmovies.com/_next/data/{buildId}/en/theatres.json")
                    };
                var result = await _httpClient.SendAsync(request);
                if (!result.IsSuccessStatusCode)
                {
                    throw new ApplicationException("Could not retrieve Regal theater list.");
                }
                var parsedContent = await JsonSerializer.DeserializeAsync<
                    RegalPageProps<RegalTheaterProps>>(await result.Content.ReadAsStreamAsync());
                foreach (var theater in parsedContent.PageProps.TheaterData.Theaters)
                {
                    if (locationCodes.Contains(theater.Code))
                    {
                        returnValue.Cinemas.Add(theater.Code, new FilmScheduleModel.CinemaModel()
                            {
                                Id = theater.Code,
                                DisplayName = theater.Name,
                            });
                    }
                }
            }

            // Pull showtimes for each day at each theater
            foreach (var locationCode in locationCodes)
            {
                for (var currentDate = startDate; currentDate <= endDate;
                    currentDate = currentDate.AddDays(1))
                {
                    var request = new HttpRequestMessage()
                        {
                            RequestUri = new Uri("https://www.regmovies.com/api/getShowtimes" + 
                                $"?theatres={locationCode}" + 
                                $"&date={currentDate:MM-dd-yyyy}" + 
                                "&hoCode=&ignoreCache=false&moviesOnly=false")
                        };
                    var result = await _httpClient.SendAsync(request);
                    if (!result.IsSuccessStatusCode)
                    {
                        throw new ApplicationException("Could not retrieve Regal showtime list.");
                    }
                    var parsedContent = await JsonSerializer.DeserializeAsync<
                        RegalShowtimesResponse>(await result.Content.ReadAsStreamAsync());

                    // Update films
                    foreach (var movie in parsedContent.Movies)
                    {
                        if (!returnValue.Films.ContainsKey(movie.Code))
                        {
                            returnValue.Films.Add(movie.Code, new FilmScheduleModel.FilmModel()
                            {
                                Id = movie.Code,
                                Title = movie.Title,
                                Duration = TimeSpan.FromMinutes(movie.DurationMinutes),
                                VideoLink = movie.Media
                                    .SingleOrDefault(m => m.SubType == "Trailer_Youtube")?.Url
                            });
                        }
                    }
                    // Update showtimes
                    foreach (var showTheater in parsedContent.Shows)
                    {
                        foreach (var film in showTheater.Films)
                        {
                            foreach (var performance in film.Performances)
                            {
                                if (!returnValue.ShowingsByFilmId.ContainsKey(film.Code))
                                {
                                    returnValue.ShowingsByFilmId.Add(film.Code,
                                        new List<FilmScheduleModel.ShowingModel>());
                                }
                                returnValue.ShowingsByFilmId[film.Code].Add(
                                    new FilmScheduleModel.ShowingModel()
                                        {
                                            FilmId = film.Code,
                                            CinemaId = showTheater.TheaterCode,
                                            ShowingTime = performance.LocalShowTime,
                                            BookingLink = new Uri(
                                                "https://experience.regmovies.com/select-tickets" + 
                                                $"?site={showTheater.TheaterCode}" + 
                                                $"&id={performance.PerformanceId}")
                                        });
                            }
                        }
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

        private class RegalPageProps<T>
        {
            [JsonPropertyName("pageProps")]
            public T PageProps { get; set; }
        }

        private class RegalTheaterProps
        {
            [JsonPropertyName("theatreData")]
            public RegalTheaterData TheaterData { get; set; }

            public class RegalTheaterData
            {
                [JsonPropertyName("data")]
                public IEnumerable<RegalTheater> Theaters { get; set; }

                public class RegalTheater
                {
                    [JsonPropertyName("name")]
                    public string Name { get; set; }

                    [JsonPropertyName("theatre_code")]
                    public string Code { get; set; }
                }
            }
        }

        private class RegalShowtimesResponse
        {
            [JsonPropertyName("shows")]
            public IEnumerable<RegalShow> Shows { get; set; }

            [JsonPropertyName("movies")]
            public IEnumerable<RegalMovie> Movies { get; set; }

            public class RegalShow
            {
                [JsonPropertyName("TheatreCode")]
                public string TheaterCode { get; set; }

                [JsonPropertyName("Film")]
                public IEnumerable<RegalShowFilm> Films { get; set; }

                public class RegalShowFilm
                {
                    [JsonPropertyName("MasterMovieCode")]
                    public string Code { get; set; }

                    [JsonPropertyName("Performances")]
                    public IEnumerable<RegalShowFilmPerformance> Performances { get; set; }

                    public class RegalShowFilmPerformance
                    {
                        [JsonPropertyName("PerformanceId")]
                        public long PerformanceId { get; set; }

                        [JsonPropertyName("CalendarShowTime")]
                        public DateTime LocalShowTime { get; set; }
                    }
                }
            }

            public class RegalMovie
            {
                [JsonPropertyName("Title")]
                public string Title { get; set; }

                [JsonPropertyName("MasterMovieCode")]
                public string Code { get; set; }

                [JsonPropertyName("Description")]
                public string Description { get; set; }

                [JsonPropertyName("Duration")]
                public int DurationMinutes { get; set; }

                [JsonPropertyName("Media")]
                public IEnumerable<RegalMovieMedia> Media { get; set; }

                public class RegalMovieMedia
                {
                    [JsonPropertyName("Type")]
                    public string Type { get; set; }

                    [JsonPropertyName("SubType")]
                    public string SubType { get; set; }

                    [JsonPropertyName("Url")]
                    public Uri Url { get; set; }
                }
            }
        }
    }
}