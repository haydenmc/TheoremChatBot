using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Theorem.ChatServices;

namespace Theorem.Models
{
    public class ChatMessageModel
    {
        public virtual string Id { get; set; }

        public virtual ChatServiceKind Provider { get; set; }

        public virtual string ProviderInstance { get; set; }

        public virtual string AuthorId { get; set; }

        public virtual string AuthorAlias { get; set; }

        public virtual string AuthorDisplayName { get; set; }

        public virtual string Body { get; set; }

        public virtual Dictionary<string, string> FormattedBody { get; set; }

        public virtual string ChannelId { get; set; }

        public virtual DateTimeOffset TimeSent { get; set; }

        public virtual string ThreadingId { get; set; }

        public virtual IEnumerable<AttachmentModel> Attachments { get; set; }

        public virtual IChatServiceConnection FromChatServiceConnection { get; set; }

        public virtual bool IsFromTheorem { get; set; }

        public virtual bool IsMentioningTheorem { get; set; }

        public virtual bool IsPrivateMessage { get; set; }
    }
}