using Newtonsoft.Json;

namespace Theorem.Models
{
    public class SlackEventModel
    {
        [JsonProperty("type")]
        public string EventType { get; set; }
    }
}