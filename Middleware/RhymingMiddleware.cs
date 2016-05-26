using System;
using Theorem.Models;
using Theorem.Providers;

namespace Theorem.Middleware
{
    public class RhymingMiddleware : IMiddleware
    {
        private SlackProvider _slackProvider { get; set; }
        
        public RhymingMiddleware(SlackProvider slackProvider)
        {
            _slackProvider = slackProvider;
        }
        
        public MiddlewareResult ProcessMessage(MessageEventModel message)
        {
            if (message.UserId != _slackProvider.Self.Id && message.Text.Contains("ing"))
            {
                Console.WriteLine($"Rhyme for {message.Text}.");
                _slackProvider.SendMessageToChannelId(message.ChannelId, message.Text.Replace("ing", "ong")).Wait();
                return MiddlewareResult.Stop;
            }
            return MiddlewareResult.Continue;
        }
    }
}