using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;using Microsoft.Extensions.Logging;
using Theorem.ChatServices;
using Theorem.Models;
using Theorem.Providers;
using Theorem.Utility;

namespace Theorem.Middleware
{
    public class ListMiddleware : IMiddleware, ISummonable
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
                return "list";
            } 
        }

        public string Usage
        {
            get
            {
                return @"list
  List all currently available bot commands. Use ""<verb> help"" to get more info about individual commands.";
            }
        }

        /// <summary>
        /// Alphabetically ordered list of running middlewares to print.
        /// </summary>
        private readonly Dictionary<string, string> _middlewareVerbs;

        private IEnumerable<IChatServiceConnection> chatServiceConnections;

        public ListMiddleware(ILogger<ListMiddleware> logger,
            ConfigurationSection configuration,
            IEnumerable<IChatServiceConnection> chatServiceConnections,
            BotMetadataProvider botMetadataProvider)
        {
            _middlewareVerbs = new Dictionary<string, string>();

            foreach(var middleware in botMetadataProvider.RunningMiddlewares)
            {
                if(middleware.IsSummonable && middleware.Enabled)
                {
                    _middlewareVerbs.Add(middleware.SummonVerb, middleware.Name);
                }
            }

            this.chatServiceConnections = chatServiceConnections;
        }

        public MiddlewareResult ProcessMessage(ChatMessageModel message)
        {
            // Ignore messages from ourself
            if (message.IsFromTheorem || !message.IsMentioningTheorem)
            {
                return MiddlewareResult.Continue;
            }

            var serviceConnection = message.FromChatServiceConnection;

            var verbList = string.Join('\n',_middlewareVerbs.ToList().Select(kvp => kvp.Key + " -> " + kvp.Value));

            serviceConnection
                .SendMessageToChannelIdAsync(
                    message.ChannelId,
                    "Available commands: \n\n" + verbList
                )
                .Wait();
            return MiddlewareResult.Stop;
        }
    }
}