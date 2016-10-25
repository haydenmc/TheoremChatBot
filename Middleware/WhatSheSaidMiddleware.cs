using System;
using Microsoft.Extensions.Configuration;
using Theorem.Models;
using Theorem.Models.Events;
using Theorem.Providers;

namespace Theorem.Middleware
{
    public class WhatSheSaidMiddleware : Middleware
    {
        private const double _chanceOfSheSaid = 0.01;
        
        public WhatSheSaidMiddleware(SlackProvider slackProvider, Func<ApplicationDbContext> dbContext, IConfigurationRoot configuration)
            : base(slackProvider, dbContext, configuration)
        {
            // This space intentionally left blank
        }

        public override MiddlewareResult ProcessMessage(MessageEventModel message)
        {
            Random random = new Random();
            if (message.SlackUserId != _slackProvider.Self.Id && random.NextDouble() < _chanceOfSheSaid)
            {
                var user = _slackProvider.GetUserBySlackId(message.SlackUserId);
                _slackProvider.SendMessageToChannelId(message.SlackChannelId, $"<@{message.SlackUserId}> That's what she said.").Wait();
            }
            return MiddlewareResult.Continue;
        }
    }
}