using Theorem.Models;

namespace Theorem.Middleware
{
    public interface IMiddleware
    {
        MiddlewareResult ProcessMessage(ChatMessageModel message);
    }
}