using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Theorem.Models.Events;

namespace Theorem.Models
{
    public class UserModel
    {
        public Guid Id { get; set; }
        public string SlackId { get; set; }
        public string Name { get; set; }
        public bool Deleted { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string RealName { get; set; }
        public string Email { get; set; }
        public string Skype { get; set; }
        public string Phone { get; set; }
        public string Image512 { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsOwner { get; set; }
        public bool IsPrimaryOwner { get; set; }
        public bool IsUnrestricted { get; set; }
        public bool IsUltraUnrestricted { get; set; }
        public bool HasTwoFactorAuth { get; set; }
        public string TwoFactorType { get; set; }
        public bool HasFiles { get; set; }
        [InverseProperty("Member")]
        public virtual ICollection<ChannelMemberModel> Channels { get; set; }
        [InverseProperty("User")]
        public virtual ICollection<MessageEventModel> Messages { get; set; }
    }
}