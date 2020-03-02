using Newtonsoft.Json;

namespace Theorem.Models.Slack.Events
{
    public class SlackPresenceChangeEventModel : SlackEventModel
    {
        [JsonProperty("presence")]
        public string Presence { get; set;}
    }
}