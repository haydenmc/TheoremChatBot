using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Theorem.ChatServices;
using Theorem.Models;

namespace Theorem.Middleware
{
    public class MiddlewarePipeline
    {
        private ILogger<MiddlewarePipeline> _logger;
        private IEnumerable<IChatServiceConnection> _chatServiceConnections;

        private IEnumerable<IMiddleware> _middleware;

        private IDictionary<string, Type[]> _chatServiceConnectionMiddlewares;

        
        public MiddlewarePipeline(
            ILogger<MiddlewarePipeline> logger,
            IEnumerable<IChatServiceConnection> chatServiceConnections,
            IEnumerable<IMiddleware> middleware,
            IDictionary<string, Type[]> chatServiceConnectionMiddlewares)
        {
            _logger = logger;
            _chatServiceConnections = chatServiceConnections;
            _middleware = middleware;
            _chatServiceConnectionMiddlewares = chatServiceConnectionMiddlewares;
            foreach (var chatServiceConnection in _chatServiceConnections)
            {
                chatServiceConnection.NewMessage += NewMessage;
            }
        }

        private void NewMessage(object sender, ChatMessageModel message)
        {
            var chatServiceConnection = sender as IChatServiceConnection;
            var middlewareTypes = _chatServiceConnectionMiddlewares[chatServiceConnection.Name];
            foreach (var middlewareType in middlewareTypes)
            {
                var middlewareInstance = 
                    _middleware.SingleOrDefault(m => middlewareType == m.GetType());
                _logger.LogDebug(
                    "Processing middleware {middleware} for chat service {chatservice}...",
                    middlewareInstance.GetType().ToString(),
                    chatServiceConnection.Name);
                if (middlewareInstance != null)
                {
                    try
                    {
                        var result = middlewareInstance.ProcessMessage(message);
                        if (result == MiddlewareResult.Stop)
                        {
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError("Exception while executing '{middleware}':{exception}",
                            middlewareType.ToString(),
                            e.Message);
                    }
                }
            }
        }
    }
}