using System.Collections.Generic;
using Newtonsoft.Json;

namespace Theorem.Models.Mattermost
{
    public class MattermostEventBroadcastModel
    {
        [JsonProperty("omit_users")]
        public Dictionary<string, bool> OmitUsers { get; set; }

        [JsonProperty("user_id")]
        public string UserId { get; set; }

        [JsonProperty("channel_id")]
        public string ChannelId { get; set; }

        [JsonProperty("team_id")]
        public string TeamId { get; set; }
    }
}