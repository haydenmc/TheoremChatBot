using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Theorem.ChatServices;
using Theorem.Models;
using Theorem.Providers;

namespace Theorem.Middleware
{
    public class InfoMiddleware : IMiddleware, ISummonable
    {
        /// <summary>
        /// Pattern used to match messages.
        /// </summary>
        public string MentionRegex
        { 
            get 
            {
                return @"(ordered|disabled)?";
            } 
        }

        /// <summary>
        /// verb used to summon this middleware.
        /// </summary>
        public static string SummonVerb
        { 
            get 
            {
                return "info";
            } 
        }

        public string Usage
        {
            get
            {
                return @"info (ordered|disabled)
  - ordered: print enabled middleware in execution order
  - disabled: print all disabled middleware
  - *empty*: print all middleware in alphabetical order, (d) = disabled";
            }
        }

        /// <summary>
        /// Alphabetically ordered list of running middlewares to print.
        /// </summary>
        private readonly String _alphabeticallyOrderedMiddleware;

        /// <summary>
        /// List of running middlewares ordered by execution order to print.
        /// </summary>
        private readonly String _orderedMiddleware;

        /// <summary>
        /// List of disabled middlewares to print.
        /// </summary>
        private readonly String _disabledMiddleware;

        private IEnumerable<IChatServiceConnection> chatServiceConnections;

        public InfoMiddleware(ILogger<InfoMiddleware> logger,
            ConfigurationSection configuration,
            IEnumerable<IChatServiceConnection> chatServiceConnections,
            BotMetadataProvider botMetadataProvider)
        {
            var orderedMiddlewareList = botMetadataProvider.RunningMiddlewares
                .Where(mw => mw.Enabled && mw.ExecutionOrderNumber != 0);
            var unorderedMiddlewareList = botMetadataProvider.RunningMiddlewares
                .Where(mw => mw.Enabled && mw.ExecutionOrderNumber == 0 && mw.Configured);
            var unconfiguredMiddlewareList = botMetadataProvider.RunningMiddlewares
                .Where(mw => mw.Enabled && !mw.Configured);
            var disabledMiddlewareList = botMetadataProvider.RunningMiddlewares
                .Where(mw => !mw.Enabled);

            // order middleware by 
            //   1. ordered enabled MW in execution order
            //   2. enabled MW that was not explicitly ordered
            //   3. potentially enabled, but unconfigured MW

            IEnumerable<string> orderedMiddlewareNamesList = new List<string>();
            orderedMiddlewareNamesList = orderedMiddlewareNamesList.Concat(
                orderedMiddlewareList.OrderBy(mw => mw.ExecutionOrderNumber).Select(mw => mw.Name)
            );
            orderedMiddlewareNamesList = orderedMiddlewareNamesList.Concat(
                unorderedMiddlewareList.Select(mw => mw.Name)
            );
            orderedMiddlewareNamesList = orderedMiddlewareNamesList.Concat(
                unconfiguredMiddlewareList.Select(mw => mw.Name)
            );

            _orderedMiddleware = string.Join(" -> ", orderedMiddlewareNamesList);
            _alphabeticallyOrderedMiddleware = string.Join(
                ", ", 
                botMetadataProvider.RunningMiddlewares
                    .Select(mw => mw.Enabled ? mw.Name : mw.Name + " (d)").OrderBy(str => str));
            _disabledMiddleware = string.Join(", ", disabledMiddlewareList.Select(mw => mw.Name));

            this.chatServiceConnections = chatServiceConnections;
        }

        public MiddlewareResult ProcessMessage(ChatMessageModel message)
        {
            // Ignore messages from ourself
            if (message.IsFromTheorem)
            {
                return MiddlewareResult.Continue;
            }

            // Ignore messages that don't mention us, unless they're private messages
            if (!message.IsMentioningTheorem && !message.IsPrivateMessage)
            {
                return MiddlewareResult.Continue;
            }

            var serviceConnection = message.FromChatServiceConnection;

            var printList = message.Body.Contains("ordered") ? _orderedMiddleware : 
                                (message.Body.Contains("disabled") ? _disabledMiddleware :
                                _alphabeticallyOrderedMiddleware);

            serviceConnection
                .SendMessageToChannelIdAsync(
                    message.ChannelId,
                    "Available middleware: " + printList
                )
                .Wait();
            return MiddlewareResult.Stop;
        }
    }
}