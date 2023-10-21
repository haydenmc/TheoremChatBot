using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Theorem.ChatServices;

namespace Theorem.Models
{
    public class ReactionModel
    {
        public virtual string Id { get; set; }

        public virtual string MessageId { get; set; }

        public virtual ChatServiceKind Provider { get; set; }

        public virtual string ProviderInstance { get; set; }

        public virtual string AuthorId { get; set; }

        public virtual string AuthorAlias { get; set; }

        public virtual string AuthorDisplayName { get; set; }

        public virtual string Reaction { get; set; }

        public virtual string ChannelId { get; set; }

        public virtual DateTimeOffset TimeSent { get; set; }

        public virtual IChatServiceConnection FromChatServiceConnection { get; set; }

        public virtual bool IsFromTheorem { get; set; }

        public virtual bool IsPrivateMessage { get; set; }
    }
}