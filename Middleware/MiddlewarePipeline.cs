using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Theorem.ChatServices;
using Theorem.Models;
using Theorem.Utility;

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

        private string ProcessSummonMessage(ISummonable middleware, 
                                            IChatServiceConnection chatServiceConnection, 
                                            ChatMessageModel message)
        {
            // expect exactly 1 instance of the action verb
            var verbMatchPattern = string.Concat(middleware.GetSummonVerb(), "{1}\\s?(");
            // match for message format "<bot mention> <verb> *rest of message*" and return *rest of message* to middleware
            var testPattern = string.Concat(verbMatchPattern, middleware.MentionRegex, ")");
            Regex testRegex = new Regex(testPattern, RegexOptions.IgnoreCase);

            Match match = testRegex.Match(message.Body);

            if (!match.Success)
            {
                return null;
            }

            var strippedMessageMatchGroup = match.Groups[1];
            return strippedMessageMatchGroup.Value == null ? string.Empty 
                                                           : strippedMessageMatchGroup.Value;
        }

        private bool TestHelpMessage(ISummonable middleware, 
                                     IChatServiceConnection chatServiceConnection, 
                                     ChatMessageModel message)
        {
            // expect exactly 1 instance of the action verb
            var helpMatchPattern = string.Concat(middleware.GetSummonVerb(), "{1}\\s?help\\s*");
            // match for message format "<bot mention> <verb> *rest of message*" and return *rest of message* to middleware
            Regex testRegex = new Regex(helpMatchPattern, RegexOptions.IgnoreCase);

            Match match = testRegex.Match(message.Body);

            return match.Success;
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
                        // if this is a summonable middleware, process the summon message first
                        if (middlewareInstance is ISummonable)
                        {
                            if(this.TestHelpMessage((ISummonable)middlewareInstance, 
                                                    chatServiceConnection, 
                                                    message))
                            {
                                chatServiceConnection.SendMessageToChannelIdAsync(
                                    message.ChannelId, 
                                    new ChatMessageModel()
                                    {
                                        Body = ((ISummonable)middlewareInstance).Usage
                                    })
                                    .Wait();
                                // halt pipeline execution after matching with middleware 
                                // and displaying usage info
                                break;
                            }

                            var processedMessage = 
                                this.ProcessSummonMessage((ISummonable)middlewareInstance, 
                                                          chatServiceConnection, 
                                                          message);

                            if (processedMessage == null)
                            {
                                _logger.LogInformation(
                                    "Unsuccessful summon match for {middleware} for chat service {chatservice}...",
                                    middlewareInstance.GetType().ToString(),
                                    chatServiceConnection.Name);
                                continue;
                            }

                            message.Body = processedMessage;
                        }
                        
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