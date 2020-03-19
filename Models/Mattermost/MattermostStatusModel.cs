using Newtonsoft.Json;

namespace Theorem.Models.Mattermost
{

    public class MattermostStatusModel
    {
        // [
        //   {
        //     "user_id": "string",
        //     "status": "string",
        //     "manual": true,
        //     "last_activity_at": 0
        //   }
        // ]

        [JsonProperty("user_id")]
        public string UserId { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("manual")]
        public bool Manual { get; set; }

        [JsonProperty("last_activity_at")]
        public ulong LastActivityAt { get; set; }
    }
}