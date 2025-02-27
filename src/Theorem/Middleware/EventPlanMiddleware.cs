using System;
using System.Collections.Generic;
using System.Linq
using System.Security.Cryptography;
using DateRecurrenceR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Theorem.ChatServices;
using Theorem.Models;

namespace Theorem.Middleware
{
    public class EventPlanMiddleware : IMiddleware
    {
        private enum EventModeKind
        {
            RandomVenue,
        }

        private enum EventRecurrenceKind
        {
            Daily,
            Weekly,
            Monthly,
            Yearly,
        }

        private struct EventPlan
        {
            public EventModeKind EventMode;
            public IChatServiceConnection ChatServiceConnection;
            public Func<DateTime, DateTime> GetNextOccurrenceFunc;
        }

        private readonly ILogger<EventPlanMiddleware> _logger;
        private readonly ConfigurationSection _configuration;
        private readonly IEnumerable<IChatServiceConnection> _chatServiceConnections;
        private readonly List<EventPlan> _events = [];

        public EventPlanMiddleware(
            ILogger<EventPlanMiddleware> logger,
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

        private void parseConfiguration()
        {
            var eventConfigs = _configuration.GetSection("Events").GetChildren();
            foreach (var eventConfig in eventConfigs)
            {
                var mode = eventConfig.GetValue<EventModeKind>("Mode");
                var chatServiceName = eventConfig.GetSection("PostChannel")
                    .GetValue<string>("ChatServiceName");
                var channelName = eventConfig.GetSection("PostChannel")
                    .GetValue<string>("ChannelName");
                var defaultVenues = eventConfig.GetValue<string[]>("DefaultVanues", []);
                var recurrenceSection = eventConfig.GetSection("Recurrence");
                var recurrenceKind = recurrenceSection.GetValue<EventRecurrenceKind>("Mode");
                var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(
                    recurrenceSection.GetValue<string>("TimeZone"));
                var beginDate = DateOnly.ParseExact(
                    recurrenceSection.GetValue<string>("BeginDate"), "yyyy-MM-dd");
                var atTime = TimeOnly.ParseExact(
                    recurrenceSection.GetValue<string>("AtTime"), "HH:mm");
                var interval = recurrenceSection.GetValue<int>("Interval");
                Func<DateTime, DateTime> recurrenceFunc;
                switch (recurrenceKind)
                {
                case EventRecurrenceKind.Daily:
                    recurrenceFunc = (DateTime from) => {
                        var fromDate = DateOnly.FromDateTime(from);
                        var nextDate = Recurrence.Daily(beginDate, fromDate, 1,
                            new DateRecurrenceR.Core.Interval(interval)).Current;
                        return nextDate.ToDateTime(atTime);
                    };
                    break;
                case EventRecurrenceKind.Weekly:
                    recurrenceFunc = (DateTime from) => {
                        var dayOfWeek = recurrenceSection.GetValue<DayOfWeek>("DayOfWeek");
                        var fromDate = DateOnly.FromDateTime(from);
                        var nextDate = Recurrence.Weekly(beginDate, fromDate, 1,
                            new DateRecurrenceR.Core.WeekDays(dayOfWeek), DayOfWeek.Sunday,
                            new DateRecurrenceR.Core.Interval(interval)).Current;
                        return nextDate.ToDateTime(atTime);
                    };
                    break;
                default:
                    _logger.LogWarning("Unknown recurrence kind {}", recurrenceKind);
                    continue;
                }

                var chatService = _chatServiceConnections
                    .SingleOrDefault(c => c.Name == chatServiceName);
                _events.Add(new EventPlan() {
                    EventMode = mode,
                    ChatServiceConnection = chatService,
                    GetNextOccurrenceFunc = recurrenceFunc,
                });
            }
        }

        public MiddlewareResult ProcessMessage(ChatMessageModel message)
        {
            throw new System.NotImplementedException();
        }
    }
}