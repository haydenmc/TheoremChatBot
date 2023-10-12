using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using Theorem.ChatServices;
using Theorem.Models;

namespace Theorem.Middleware;

public class EastraMiddleware : IMiddleware
{
    private readonly ILogger<EastraMiddleware> _logger;

    private readonly ConfigurationSection _configuration;

    private readonly IEnumerable<IChatServiceConnection> _chatServiceConnections;

    private readonly IList<EastraAnnounceSchedule> _announceSchedule;

    public EastraMiddleware(
        ILogger<EastraMiddleware> logger,
        ConfigurationSection configuration,
        IEnumerable<IChatServiceConnection> chatServiceConnections)
    {
        _logger = logger;
        _configuration = configuration;
        _chatServiceConnections = chatServiceConnections;
        _announceSchedule = ParseScheduleFromConfiguration(_configuration);

        parseConfiguration();
        subscribeToChatServiceConnectedEvents();
        schedulePostTimer();
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
                var voteDayOfWeekVal = scheduleConfig.GetValue<string>("VoteDayOfWeek");
                var voteTimeVal = scheduleConfig.GetValue<string>("VoteTime");
                var timeUntilAnnounceVal = scheduleConfig.GetValue<string>("TimeUntilAnnounce");
                var timeZoneVal = scheduleConfig.GetValue<string>("TimeZone");
                if ((startDateVal == null) || (voteDayOfWeekVal == null) ||
                    (voteTimeVal == null) || (timeUntilAnnounceVal == null) ||
                    (timeZoneVal == null))
                {
                    logger.LogError("Invalid weekly post schedule configuration, skipping...");
                    continue;
                }
                var startDate = DateOnly.ParseExact(startDateVal, "yyyy-MM-dd");
                var voteDayOfWeek = Enum.Parse<DayOfWeek>(voteDayOfWeekVal);
                var voteTime = TimeOnly.ParseExact(voteTimeVal, "HH:mm");
                var timeUntilAnnounce = TimeSpan.ParseExact(timeUntilAnnounceVal, "hh\\:mm", null);
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneVal);
                schedule.Add(new EastraAnnounceSchedule()
                    {
                        RecurrenceStartDate = startDate,
                        RecurrenceWeeklyInterval = weeklyIntervalVal,
                        VoteDayOfWeek = voteDayOfWeek,
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

    public class EastraAnnounceSchedule
    {
        public DateOnly RecurrenceStartDate { get; set; }
        public uint RecurrenceWeeklyInterval { get; set; }
        public DayOfWeek VoteDayOfWeek { get; set; }
        public TimeOnly VoteTime { get; set; }
        public TimeSpan TimeUntilAnnounce { get; set; }
        public TimeZoneInfo TimeZone { get; set; }
    }
}