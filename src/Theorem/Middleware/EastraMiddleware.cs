using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Theorem.ChatServices;
using Theorem.Models;

namespace Theorem.Middleware;

public class EastraMiddleware : IMiddleware
{
    private readonly ILogger<EastraMiddleware> _logger;

    private readonly ConfigurationSection _configuration;

    private readonly IEnumerable<IChatServiceConnection> _chatServiceConnections;

    private readonly Dictionary<IChatServiceConnection, string> _chatServicePostChannelId = new();

    private readonly IList<EastraAnnounceSchedule> _announceSchedule;

    public EastraMiddleware(
        ILogger<EastraMiddleware> logger,
        ConfigurationSection configuration,
        IEnumerable<IChatServiceConnection> chatServiceConnections)
    {
        _logger = logger;
        _configuration = configuration;
        _chatServiceConnections = chatServiceConnections;
        _announceSchedule = ParseScheduleFromConfiguration(logger, _configuration);
        subscribeToChatServiceConnectedEvents();
        scheduleVotePostTimers();
    }

    public MiddlewareResult ProcessMessage(ChatMessageModel message)
    {
        return MiddlewareResult.Continue;
    }

    private static IList<EastraAnnounceSchedule> ParseScheduleFromConfiguration(
        in ILogger<EastraMiddleware> logger, in ConfigurationSection configuration)
    {
        List<EastraAnnounceSchedule> schedule = new();
        var scheduleConfigs = configuration.GetSection("WeeklyPostSchedules").GetChildren();
        foreach (var scheduleConfig in scheduleConfigs)
        {
            try
            {
                var startDateVal = scheduleConfig.GetValue<string>("RecurrenceStartDate");
                var weeklyIntervalVal = scheduleConfig.GetValue<uint>(
                    "RecurrenceWeeklyInterval", 1);
                var voteTimeVal = scheduleConfig.GetValue<string>("VoteTime");
                var timeUntilAnnounceVal = scheduleConfig.GetValue<string>("TimeUntilAnnounce");
                var timeZoneVal = scheduleConfig.GetValue<string>("TimeZone");
                if ((startDateVal == null) || (voteTimeVal == null) ||
                    (timeUntilAnnounceVal == null) || (timeZoneVal == null))
                {
                    logger.LogError("Invalid weekly post schedule configuration, skipping...");
                    continue;
                }
                var startDate = DateOnly.ParseExact(startDateVal, "yyyy-MM-dd");
                var voteTime = TimeOnly.ParseExact(voteTimeVal, "HH:mm");
                var timeUntilAnnounce = TimeSpan.ParseExact(timeUntilAnnounceVal, "hh\\:mm", null);
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneVal);
                schedule.Add(new EastraAnnounceSchedule()
                    {
                        RecurrenceStartDate = startDate,
                        RecurrenceWeeklyInterval = weeklyIntervalVal,
                        VoteTime = voteTime,
                        TimeUntilAnnounce = timeUntilAnnounce,
                        TimeZone = timeZone,
                    });
            }
            catch
            {
                logger.LogError("Error while parsing weekly post schedule configuration, " +
                    "skipping...");
                    continue;
            }
        }
        return schedule;
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

    private void scheduleVotePostTimers()
    {
        foreach (var schedule in _announceSchedule)
        {

        }
    }

    private static DateTimeOffset GetNextVoteOccurrence(in EastraAnnounceSchedule schedule)
    {
        // Get the week # of the start date, compare that to the current week to figure
        // out when the next occurrence should be.
        var dateFormatInfo = DateTimeFormatInfo.CurrentInfo;
        var currentWeekNum = dateFormatInfo.Calendar.GetWeekOfYear(
            schedule.RecurrenceStartDate.ToDateTime(schedule.VoteTime),
            dateFormatInfo.CalendarWeekRule, dateFormatInfo.FirstDayOfWeek);
        //DateTimeOffset.Now.CompareTo(DateTimeOffset.Now.AddDays(-12).)

        throw new NotImplementedException();
    }

    public class EastraAnnounceSchedule
    {
        public TimeZoneInfo TimeZone { get; set; }
        public DateOnly RecurrenceStartDate { get; set; }
        public uint RecurrenceWeeklyInterval { get; set; }
        public TimeOnly VoteTime { get; set; }
        public TimeSpan TimeUntilAnnounce { get; set; }
    }
}