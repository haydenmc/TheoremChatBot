using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Theorem.Models;
using Theorem.Models.Events;
using Theorem.Providers;

namespace Theorem.Middleware
{
    public class SeenMiddleware : IMiddleware
    {
        /// <summary>
        /// Reference to the Slack provider.
        /// </summary>
        private SlackProvider _slackProvider { get; set;}

        /// <summary>
        /// Returns a new db context to use for interacting with the database
        /// </summary>
        private Func<ApplicationDbContext> _dbContext { get; set; }

        /// <summary>
        /// Pattern used to match messages
        /// </summary>
        private const string _messagePattern = @".*<@{0}>.*seen{{0,1}}.*<@([A-Za-z\d]+)>";

        /// <summary>
        /// Regex used to match messages
        /// </summary>
        private Regex _messageRegex { get; set; }

        /// <summary>
        /// Error message used when a user could not be found.
        /// </summary>
        private const string _userNotFoundErrorMessage = @"Sorry <@{0}>, I couldn't find a user by the name of @{1}.";

        /// <summary>
        /// Error message used when no events could be found.
        /// </summary>
        private const string _noEventsFoundErrorMessage = @"Sorry <@{0}>, I couldn't find any recent activity by <@{1}>.";

        /// <summary>
        /// Response used when the last seen event is MessageEvent.
        /// </summary>
        private const string _messageEventMessage = @"<@{0}>: I last saw <@{1}> saying ""{2}"" in <#{3}>.";

        /// <summary>
        /// Response used when the last seen event is TypingEvent.
        /// </summary>
        private const string _typingEventMessage = @"<@{0}>: I last saw <@{1}> typing in <#{2}>.";

        /// <summary>
        /// Response used when the last seen event is PresenceChangeEvent.
        /// </summary>
        private const string _presenceChangeEventMessage = @"<@{0}>: I last saw <@{1}> changing their presence to '{2}'.";

        /// <summary>
        /// Response used for any other event.
        /// </summary>
        private const string _otherEventMessage = @"<@{0}>: I last saw <@{1}> performing a '{2}' Slack event.";

        public SeenMiddleware(SlackProvider slackProvider, Func<ApplicationDbContext> dbContext)
        {
            _slackProvider = slackProvider;
            _dbContext = dbContext;
        }

        public MiddlewareResult ProcessMessage(MessageEventModel message)
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
            var match = _messageRegex.Match(message.Text);
            if (match.Success)
            {
                var specifiedUserSlackId = match.Groups[1].Captures[0].Value;
                using (var db = _dbContext())
                {
                    var targetUser = db.Users.SingleOrDefault(u => u.SlackId == specifiedUserSlackId);
                    if (targetUser == null)
                    {
                        _slackProvider
                            .SendMessageToChannelId(
                                message.SlackChannelId,
                                String.Format(_userNotFoundErrorMessage, message.User.SlackId, specifiedUserSlackId)
                            )
                            .Wait();
                        return MiddlewareResult.Stop;
                    }
                    // Find latest activity by specified user
                    var latestEvent = db.Events
                        .Include(e => e.Channel)
                        .Include(e => e.User)
                        .Where(e => e.UserId == targetUser.Id).OrderByDescending(e => e.TimeReceived).FirstOrDefault();
                    if (latestEvent == null)
                    {
                        _slackProvider
                            .SendMessageToChannelId(
                                message.SlackChannelId,
                                String.Format(_noEventsFoundErrorMessage, message.User.SlackId, targetUser.SlackId)
                            )
                            .Wait();
                        return MiddlewareResult.Stop;
                    }
                    if (latestEvent is MessageEventModel)
                    {
                        var messageEvent = latestEvent as MessageEventModel;
                        _slackProvider
                            .SendMessageToChannelId(
                                message.SlackChannelId,
                                String.Format(
                                    _messageEventMessage,
                                    message.User.SlackId,
                                    targetUser.SlackId,
                                    messageEvent.Text,
                                    messageEvent.Channel.SlackId
                                )
                            )
                            .Wait();
                        return MiddlewareResult.Stop;
                    }
                    else if (latestEvent is TypingEventModel)
                    {
                        var typingEvent = latestEvent as TypingEventModel;
                        _slackProvider
                            .SendMessageToChannelId(
                                message.SlackChannelId,
                                String.Format(
                                    _typingEventMessage,
                                    message.User.SlackId,
                                    targetUser.SlackId,
                                    typingEvent.Channel.SlackId
                                )
                            )
                            .Wait();
                        return MiddlewareResult.Stop;
                    }
                    else if (latestEvent is PresenceChangeEventModel)
                    {
                        var presenceChangeEvent = latestEvent as PresenceChangeEventModel;
                        _slackProvider
                            .SendMessageToChannelId(
                                message.SlackChannelId,
                                String.Format(
                                    _presenceChangeEventMessage,
                                    message.User.SlackId,
                                    targetUser.SlackId,
                                    presenceChangeEvent.Presence
                                )
                            )
                            .Wait();
                        return MiddlewareResult.Stop;
                    }
                    else
                    {
                        _slackProvider
                            .SendMessageToChannelId(
                                message.SlackChannelId,
                                String.Format(
                                    _otherEventMessage,
                                    message.User.SlackId,
                                    targetUser.SlackId,
                                    latestEvent.SlackEventType
                                )
                            )
                            .Wait();
                        return MiddlewareResult.Stop;
                    }
                }
            }
            return MiddlewareResult.Continue;
        }
    }
}