using Newtonsoft.Json;

namespace Theorem.Models.Mattermost
{
    public class MattermostAuthChallengeModel
    {
        public class AuthChallengeData
        {
            [JsonProperty("token")]
            public string Token { get; set; }
        }

        [JsonProperty("seq")]
        public int Seq { get; set; }

        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("data")]
        public AuthChallengeData Data { get; set; }

        
    }
}