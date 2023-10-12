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

    private ConfigurationSection _configuration;

    private IEnumerable<IChatServiceConnection> _chatServiceConnections;

    private DayOfWeek _votePostDayOfWeek;
    private TimeSpan _votePostTimeOfDay;
    private DayOfWeek _announcePostDayOfWeek;
    private TimeSpan _announcePostTimeOfDay;

    public EastraMiddleware(
        ILogger<EastraMiddleware> logger,
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
}