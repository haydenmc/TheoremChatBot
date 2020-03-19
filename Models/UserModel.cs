using System.ComponentModel.DataAnnotations.Schema;
using Theorem.ChatServices;

namespace Theorem.Models
{
    public class UserModel
    {
        public string Id { get; set; }

        public ChatServiceKind Provider { get; set; }

        public string Name { get; set; }

        public string DisplayName { get; set; }

        [NotMapped]
        public virtual IChatServiceConnection FromChatServiceConnection { get; set; }
    }
}