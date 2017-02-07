using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Theorem.Models;
using Theorem.Models.Events;
using Theorem.Providers;

namespace Theorem.Middleware
{
    public class InfoMiddleware : Middleware
    {
        /// <summary>
        /// Pattern used to match messages when in a private context.
        /// </summary>
        private const string _privateMessagePattern = @".*info{0,1}(ordered)?"; 

        /// <summary>
        /// Regex used to match messages in a private context.
        /// </summary>
        private Regex _privateMessageRegex { get; set; }

        /// <summary>
        /// Pattern used to match messages.
        /// </summary>
        private const string _messagePattern = @".*<@{0}>.*info{{0,1}}(ordered)?";

        /// <summary>
        /// Regex used to match messages.
        /// </summary>
        private Regex _messageRegex { get; set; }

        /// <summary>
        /// Alphabetically ordered list of running middlewares to print.
        /// </summary>
        private readonly String _alphabeticallyOrderedMiddleware;

        /// <summary>
        /// List of running middlewares ordered by execution order to print.
        /// </summary>
        private readonly String _orderedMiddleware;

        public InfoMiddleware(SlackProvider slackProvider, Func<ApplicationDbContext> dbContext, IConfigurationRoot configuration, BotInfoProvider botInfoProvider)
            : base(slackProvider, dbContext, configuration)
        {
            var middlewareNamesList = botInfoProvider.RunningMiddlewares.Reverse();
            _orderedMiddleware = string.Join(" -> ", middlewareNamesList);
            _alphabeticallyOrderedMiddleware = string.Join(", ", middlewareNamesList.OrderBy(str => str));

        }

        public override MiddlewareResult ProcessMessage(MessageEventModel message)
        {
            // Ignore messages from ourself
            if (message.User.SlackId == _slackProvider.Self.Id)
            {
                return MiddlewareResult.Continue;
            }
            // Compile Regex (can't be done in constructor, as SlackProvider does not yet exist)
            if (_messageRegex == null)
            {
                _messageRegex = new Regex(String.Format(_messagePattern, _slackProvider.Self.Id), RegexOptions.IgnoreCase);
            }
            if (_privateMessageRegex == null)
            {
                _privateMessageRegex = new Regex(_privateMessagePattern, RegexOptions.IgnoreCase);
            }
            // Match based on channel/im message
            Match match;
            if (_slackProvider.GetChannelBySlackId(message.SlackChannelId) != null)
            {
                match = _messageRegex.Match(message.Text);
            }
            else if (_slackProvider.ImsById.ContainsKey(message.SlackChannelId))
            {
                match = _privateMessageRegex.Match(message.Text);
            }
            else
            {
                return MiddlewareResult.Continue;
            }

            if (!match.Success)
            {
                return MiddlewareResult.Continue;
            }

            var printList = message.Text.Contains("ordered") ? _orderedMiddleware : _alphabeticallyOrderedMiddleware;

            _slackProvider
                .SendMessageToChannelId(
                    message.SlackChannelId,
                    "Available middleware: " + printList
                )
                .Wait();
            return MiddlewareResult.Stop;
        }
    }
}