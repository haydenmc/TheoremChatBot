using Newtonsoft.Json;

namespace Theorem.Models.Mattermost
{
    public class MattermostAuthChallengeDataModel
    {
        [JsonProperty("token")]
        public string Token { get; set; }
    }
}