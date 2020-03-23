using Theorem.Models;

namespace Theorem.Middleware
{
    public class EchoMiddleware :
        IMiddleware, ISummonable
    {
        public string MentionRegex
        {
            get
            {
                return ".*";
            }
        }

        public static string SummonVerb
        {
            get
            {
                return "echo";
            }
        }

        public string Usage
        {
            get
            {
                return @"echo <string>
  Have theorem repeat the provided string";
            }
        }

        public MiddlewareResult ProcessMessage(ChatMessageModel message)
        {
            if (message.IsMentioningTheorem && !message.IsFromTheorem)
            {
                message.FromChatServiceConnection?.SendMessageToChannelIdAsync(
                    message.ChannelId,
                    message.Body);
            }
            return MiddlewareResult.Continue;
        }
    }
}