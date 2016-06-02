using Newtonsoft.Json;

namespace Theorem.Models.Events
{
    public class PresenceChangeEventModel : EventModel
    {
        [JsonProperty("user")]
        public string SlackUserId { get; set; }
        
        [JsonProperty("presence")]
        public string Presence { get; set;}
    }
}