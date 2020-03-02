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
        private IChatProvider _chatProvider { get; set; }
        private IEnumerable<Middleware> _middleware { get; set; }
        
        public MiddlewarePipeline(IChatProvider chatProvider, IEnumerable<Middleware> middleware)
        {
            _chatProvider = chatProvider;
            _chatProvider.NewMessage += NewMessage;
            _middleware = middleware;
        }

        private void NewMessage(object sender, MessageEventModel message)
        {
            if(message.Text == null)
            {
                // if there is no text this is a reaction to a message
                // ignore that for now, if there is new middleware that needs to respond to this
                // figure out a better way to protect existing middleware from nullpointer exceptions
                return;
            }
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