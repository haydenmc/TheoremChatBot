using System;
using System.Collections.Generic;
using Theorem.ChatServices;
using Theorem.Models;

namespace Theorem.Middleware
{
    public class MiddlewarePipeline
    {
        private IEnumerable<IChatServiceConnection> _chatServiceConnections { get; set; }
        private IEnumerable<IMiddleware> _middleware { get; set; }
        
        public MiddlewarePipeline(
            IEnumerable<IChatServiceConnection> chatServiceConnections,
            IEnumerable<IMiddleware> middleware)
        {
            _chatServiceConnections = chatServiceConnections;
            foreach (var chatServiceConnection in _chatServiceConnections)
            {
                chatServiceConnection.NewMessage += NewMessage;
            }
            _middleware = middleware;
        }

        private void NewMessage(object sender, ChatMessageModel message)
        {
            foreach (var middleware in _middleware)
            {
                try 
                {
                    var result = middleware.ProcessMessage(message);
                    if (result == MiddlewareResult.Stop)
                    {
                        break;
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(
                        $"Exception while executing {middleware.GetType().Name}:\n" + 
                        $"{e.Message}");
                }
            }
        }
    }
}