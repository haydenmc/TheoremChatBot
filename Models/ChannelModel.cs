using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Theorem.Models.Events;

namespace Theorem.Models
{
    public class ChannelModel
    {
        public Guid Id { get; set; }
        public string SlackId { get; set; }
        public string Name { get; set; }
        public bool IsChannel { get; set; }
        public DateTimeOffset TimeCreated { get; set; }
        public string CreatorSlackId { get; set; }
        [ForeignKey("CreatorId")]
        public UserModel Creator { get; set; }
        public Guid CreatorId { get; set; }
        public bool IsArchived { get; set; }
        public bool IsGeneral { get; set; }
        [InverseProperty("Channel")]
        public virtual ICollection<ChannelMemberModel> ChannelMembers { get; set; }
        public string Topic { get; set; }
        public string Purpose { get; set; }
        public bool IsMember { get; set; }
        public DateTimeOffset TimeLastRead { get; set; }
        [InverseProperty("Channel")]
        public virtual ICollection<MessageEventModel> Messages { get; set; }
    }
}