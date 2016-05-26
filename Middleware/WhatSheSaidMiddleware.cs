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
            if (message.UserId != _slackProvider.Self.Id && random.NextDouble() < _chanceOfSheSaid)
            {
                var user = _slackProvider.UsersById[message.UserId];
                _slackProvider.SendMessageToChannelId(message.ChannelId, $"@{user.Name} That's what she said.").Wait();
            }
            return MiddlewareResult.Continue;
        }
    }
}