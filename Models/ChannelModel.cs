using System.Collections.Generic;

namespace Theorem.Models
{
    public class ChannelModel
    {
        // Unique ID for the channel on the service
        public string Id { get; set; }

        // Some services have an "alias" for the channel that is somewhere between an ID
        // and a name. Ex. "#general:warmitup.tv" on Matrix.
        public string Alias { get; set; }

        // Friendly display name for the channel
        public string DisplayName { get; set; }

        // List of users in this channel
        public ICollection<UserModel> Users { get; set; }
    }
}