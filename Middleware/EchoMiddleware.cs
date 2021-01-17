using Theorem.Models;

namespace Theorem.Middleware
{
    public class EchoMiddleware :
        IMiddleware, ISummonable
    {

        /// <summary>
        /// Pattern used to match messages.
        /// </summary>
        public string MentionRegex
        { 
            get 
            {
                return @".*";
            } 
        }

        /// <summary>
        /// verb used to summon this middleware.
        /// </summary>
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
                return @"echo <text>
  have Theorem repeat the text to you";
            }
        }

        public MiddlewareResult ProcessMessage(ChatMessageModel message)
        {
            if (message.IsMentioningTheorem && !message.IsFromTheorem)
            {
                message.FromChatServiceConnection?.SendMessageToChannelIdAsync(
                    message.ChannelId,
                    message);
            }
            return MiddlewareResult.Continue;
        }
    }
}