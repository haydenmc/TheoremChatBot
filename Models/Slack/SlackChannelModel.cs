using Newtonsoft.Json;

namespace Theorem.Models.Slack
{
    public class SlackChannelModel
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        
        [JsonProperty("created")]
        public string CreationTime { get; set; }
        
        [JsonProperty("num_members")]
        public int MemberCount { get; set; }
    }
}