using System;
using System.ComponentModel.DataAnnotations.Schema;
using Theorem.ChatServices;

namespace Theorem.Models
{
    public class ChatMessageModel
    {
        /* Content from Chat Service */
        public virtual string Id { get; set; }

        public virtual ProviderKind Provider { get; set; }

        public virtual string ProviderInstance { get; set; }

        public virtual string AuthorId { get; set; }

        public virtual string Body { get; set; }

        public virtual string ChannelId { get; set; }

        public virtual DateTimeOffset TimeSent { get; set; }

        public virtual string ThreadingId { get; set; }

        /* Utility */
        [NotMapped]
        public virtual IChatServiceConnection FromChatServiceConnection { get; set; }
        
        [NotMapped]
        public virtual bool IsFromSelf { get; set; }

        [NotMapped]
        public virtual bool IsMentioningMe { get; set; }
    }
}