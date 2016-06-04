using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Theorem.Models
{
    public class ChannelMemberModel
    {
        [ForeignKey("ChannelId")]
        [InverseProperty("ChannelMembers")]
        public ChannelModel Channel { get; set; }
        public Guid ChannelId { get; set; }
        [ForeignKey("MemberId")]
        [InverseProperty("Channels")]
        public UserModel Member { get; set; }
        public Guid MemberId { get; set; }
    }
}