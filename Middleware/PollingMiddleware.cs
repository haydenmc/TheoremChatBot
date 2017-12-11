using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Theorem.Models;
using Theorem.Providers;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json;
using Theorem.Models.Events;
using Theorem.Models.Slack;
using Microsoft.EntityFrameworkCore;

namespace Theorem.Middleware
{
    public class PollingMiddleware : Middleware
    {
        /// <summary>
        /// Pattern used to match messages.
        /// </summary>
        private const string _messagePattern = @".*<@{0}>.*poll\w*(.+)";

        /// <summary>
        /// Regex used to match messages.
        /// </summary>
        private Regex _messageRegex { get; set; }

        /// <summary>
        /// Poll message template
        /// </summary>
        private const string _pollTemplateMessage = @"<!channel> {0}";

        /// <summary>
        /// Poll message RegEx
        /// </summary>
        private Regex _pollTemplateRegex;

        /// <summary>
        /// Pattern used to match emojis
        /// </summary>
        private const string _emojiPattern = @":([a-zA-Z\d_+-]+):";

        /// <summary>
        /// Emoji RegEx
        /// </summary>
        private Regex _emojiRegex;

        public PollingMiddleware(SlackProvider slackProvider, Func<ApplicationDbContext> dbContext, IConfigurationRoot configuration)
            : base(slackProvider, dbContext, configuration)
        {
        }

        private MiddlewareResult TestMessageForThreadReply(MessageEventModel message)
        {
            MessageEventModel threadParent = null;
            using(var db = _dbContext())
            {
                threadParent = db.MessageEvents.Where(m => m.SlackTimeSent.Equals(message.SlackThreadId))
                    ?.Include(m => m.User)
                    .Include(m => m.Channel)
                    .SingleOrDefault();
            }

            if(threadParent != null && threadParent.User.SlackId == _slackProvider.Self.Id)
            {
                if(_pollTemplateRegex == null)
                {
                    _pollTemplateRegex = new Regex(
                        String.Format(_pollTemplateMessage, ".*"), RegexOptions.IgnoreCase);
                }

                Match pollMatch = _pollTemplateRegex.Match(threadParent.Text);
                if(pollMatch.Success)
                {
                    return ParseAndAddEmoji(message, threadParent);
                }
            }

            return MiddlewareResult.Continue;
        }

        private MiddlewareResult ParseAndAddEmoji(MessageEventModel reply, MessageEventModel parent)
        {
            if(_emojiRegex == null)
            {
                _emojiRegex = new Regex(String.Format(_emojiPattern), RegexOptions.IgnoreCase);
            }
            Match emojiMatch = _emojiRegex.Match(reply.Text);
            if(emojiMatch.Success)
            {
                string emoji = emojiMatch.Groups[1].Captures[0].Value;
                _slackProvider.ReactToMessage(emoji, parent).Wait();
                return MiddlewareResult.Stop;
            }
            return MiddlewareResult.Continue;
        }
        
        public override MiddlewareResult ProcessMessage(MessageEventModel message)
        {
            if (message.SlackUserId == _slackProvider.Self.Id)
            {
                return MiddlewareResult.Continue;
            }
            // check if message is a reply to a thread and whether the original message was a poll
            if (message.SlackThreadId != 0 && !message.SlackTimeSent.Equals(message.SlackThreadId))
            {
                return TestMessageForThreadReply(message);
            }
            // Compile Regex (can't be done in constructor, as SlackProvider does not yet exist)
            if (_messageRegex == null)
            {
                _messageRegex = new Regex(String.Format(_messagePattern, _slackProvider.Self.Id), RegexOptions.IgnoreCase);
            }        
            // Match based on channel/im message
            Match match;
            if (_slackProvider.GetChannelBySlackId(message.SlackChannelId) != null)
            {
                match = _messageRegex.Match(message.Text);
            }
            else
            {
                return MiddlewareResult.Continue;
            }
            // Extract poll topic
            String pollTopic;
            if (match.Success)
            {
                pollTopic = match.Groups[1].Captures[0].Value;
            }
            else
            {
                return MiddlewareResult.Continue;
            }

            List<SlackAttachmentModel> attachments = new List<SlackAttachmentModel>();

            SlackAttachmentModel attachment = new SlackAttachmentModel();

            SlackAttachmentFieldModel field = new SlackAttachmentFieldModel();

            field.Value = "To add a poll option, reply to the thread started by this message and make sure you include an emoji.";
            field.Short = false;

            attachment.Fields = new List<SlackAttachmentFieldModel>();
            attachment.Fields.Add(field);

            attachments.Add(attachment);
            
            _slackProvider
                .SendMessageToChannelId(
                    message.Channel.SlackId,
                    String.Format(_pollTemplateMessage, pollTopic),
                    attachments
                )
                .Wait();

            return MiddlewareResult.Stop;
        }
    }
}