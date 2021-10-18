using System.ComponentModel.DataAnnotations.Schema;
using Theorem.ChatServices;

namespace Theorem.Models
{
    public class UserModel
    {
        public enum PresenceKind
        {
            Offline = 0,
            Online
        }

        // Unique ID for this user on the service
        public string Id { get; set; }

        // Whether or not this user is us
        public bool IsTheorem { get; set; }

        // Which provider does this user belong to
        public ChatServiceKind Provider { get; set; }

        // User's "alias," or username used to uniquely identify them.
        // Ex. "#hayden:warmitup.tv" on Matrix.
        public string Alias { get; set; }

        // Friendly display name
        public string DisplayName { get; set; }

        // Whether the user is online or otherwise
        public PresenceKind Presence { get; set; }

        // Which chat service instance produced this user model
        public virtual IChatServiceConnection FromChatServiceConnection { get; set; }
    }
}