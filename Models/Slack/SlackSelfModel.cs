using Newtonsoft.Json;

namespace Theorem.Models.Slack
{
    public class SlackSelfModel
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        
        [JsonProperty("name")]
        public string Name { get; set; }
    }
}