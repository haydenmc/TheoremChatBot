using Theorem.Models;

namespace Theorem.Middleware
{
    public class EchoMiddleware :
        IMiddleware
    {
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