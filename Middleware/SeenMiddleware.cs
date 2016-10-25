using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Theorem.Models;
using Theorem.Models.Events;
using Theorem.Providers;

namespace Theorem.Middleware
{
    public class SeenMiddleware : Middleware
    {
        /// <summary>
        /// Pattern used to match messages when in a private context.
        /// </summary>
        private const string _privateMessagePattern = @".*seen{0,1}.*<@([A-Za-z\d]+)>"; 

        /// <summary>
        /// Regex used to match messages in a private context.
        /// </summary>
        private Regex _privateMessageRegex { get; set; }

        /// <summary>
        /// Pattern used to match messages.
        /// </summary>
        private const string _messagePattern = @".*<@{0}>.*seen{{0,1}}.*<@([A-Za-z\d]+)>";

        /// <summary>
        /// Regex used to match messages.
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
        private const string _messageEventMessage = @"<@{0}>: I last saw <@{1}> {2} saying ""{3}"" in <#{4}>.";

        /// <summary>
        /// Response used when the last seen event is TypingEvent.
        /// </summary>
        private const string _typingEventMessage = @"<@{0}>: I last saw <@{1}> {2} typing in <#{3}>.";

        /// <summary>
        /// Response used when the last seen event is PresenceChangeEvent.
        /// </summary>
        private const string _presenceChangeEventMessage = @"<@{0}>: I last saw <@{1}> {2} changing their presence to '{3}'.";

        /// <summary>
        /// Response used for any other event.
        /// </summary>
        private const string _otherEventMessage = @"<@{0}>: I last saw <@{1}> {2} performing a '{3}' Slack event.";

        public SeenMiddleware(SlackProvider slackProvider, Func<ApplicationDbContext> dbContext, IConfigurationRoot configuration)
            : base(slackProvider, dbContext, configuration)
        {
            // This space intentionally left blank
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
            // Extract target user
            String specifiedUserSlackId;
            if (match.Success)
            {
                specifiedUserSlackId = match.Groups[1].Captures[0].Value;
            }
            else
            {
                return MiddlewareResult.Continue;
            }
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
                var timeDelta = DateTimeOffset.Now.Subtract(latestEvent.TimeReceived);
                var agoTime = AgoTime(timeDelta);
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
                                agoTime,
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
                                agoTime,
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
                                agoTime,
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
                                agoTime,
                                latestEvent.SlackEventType
                            )
                        )
                        .Wait();
                    return MiddlewareResult.Stop;
                }
            }
        }

        /// <summary>
        /// Returns a friendly "ago" time.
        /// </summary>
        /// <param name="timespan">Time span to calculcate from</param>
        /// <returns>A friendly "x days/hours/minutes ago" string</returns>
        private static string AgoTime(TimeSpan timespan)
        {
            if (timespan.Seconds < 2)
            {
                return "just now";
            }
            else if (timespan.Minutes < 2)
            {
                return timespan.Seconds + " seconds ago";
            }
            else if (timespan.Hours < 2)
            {
                return timespan.Minutes + " minutes ago";
            }
            else if (timespan.Hours < 36)
            {
                return timespan.Hours + " hours ago";
            }
            else
            {
                return timespan.Days + " days ago";
            }
        }
    }
}