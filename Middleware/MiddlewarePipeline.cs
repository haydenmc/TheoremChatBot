using System;
using System.Collections.Generic;
using Autofac;
using Theorem.Models;
using Theorem.Models.Events;
using Theorem.Providers;

namespace Theorem.Middleware
{
    public class MiddlewarePipeline
    {
        private SlackProvider _slackProvider { get; set; }
        private IEnumerable<Middleware> _middleware { get; set; }
        
        public MiddlewarePipeline(SlackProvider slackProvider, IEnumerable<Middleware> middleware)
        {
            _slackProvider = slackProvider;
            _slackProvider.NewMessage += NewMessage;
            _middleware = middleware;
        }

        private void NewMessage(object sender, MessageEventModel message)
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
                    Console.Error.WriteLine($"Exception while executing {middleware.GetType().Name} middleware: {e.Message}");
                }
            }
        }
    }
}