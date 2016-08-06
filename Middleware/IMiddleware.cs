using System.Threading.Tasks;
using Theorem.Models;
using Theorem.Models.Events;

namespace Theorem.Middleware
{
    public interface IMiddleware
    {
        MiddlewareResult ProcessMessage(MessageEventModel message);
    }
}