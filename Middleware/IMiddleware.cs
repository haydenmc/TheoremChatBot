using System.Threading.Tasks;
using Theorem.Models;

namespace Theorem.Middleware
{
    public interface IMiddleware
    {
        MiddlewareResult ProcessMessage(MessageEventModel message);
    }
}