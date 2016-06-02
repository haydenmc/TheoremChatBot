using System;
using Theorem.Models;
using Theorem.Providers;

namespace Theorem.Middleware
{
    public class WhatSheSaidMiddleware : IMiddleware
    {
        private const double _chanceOfSheSaid = 0.01;
        
        private SlackProvider _slackProvider { get; set; }
        
        public WhatSheSaidMiddleware(SlackProvider slackProvider)
        {
            _slackProvider = slackProvider;
        }
        public MiddlewareResult ProcessMessage(MessageEventModel message)
        {
            Random random = new Random();
            if (message.SlackUserId != _slackProvider.Self.Id && random.NextDouble() < _chanceOfSheSaid)
            {
                var user = _slackProvider.UsersById[message.SlackUserId];
                _slackProvider.SendMessageToChannelId(message.SlackChannelId, $"@{user.Name} That's what she said.").Wait();
            }
            return MiddlewareResult.Continue;
        }
    }
}