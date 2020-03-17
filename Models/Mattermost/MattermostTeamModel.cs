using Newtonsoft.Json;

namespace Theorem.Models.Mattermost
{
    public class MattermostTeamModel
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("create_at")]
        public ulong CreateAt { get; set; }

        [JsonProperty("update_at")]
        public ulong UpdateAt { get; set; }

        [JsonProperty("delete_at")]
        public ulong DeleteAt { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("company_name")]
        public string CompanyName { get; set; }

        [JsonProperty("allowed_domains")]
        public string AllowedDomains { get; set; }

        [JsonProperty("invite_id")]
        public string InviteId { get; set; }

        [JsonProperty("allow_open_invite")]
        public bool AllowOpenInvite { get; set; }

        [JsonProperty("last_team_icon_update")]
        public ulong LastTeamIconUpdate { get; set; }
    }
}