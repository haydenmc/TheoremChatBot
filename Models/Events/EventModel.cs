using System;
using Newtonsoft.Json;

namespace Theorem.Models.Events
{
    public class EventModel
    {
        [JsonIgnore]
        public Guid Id { get; set; }
        
        [JsonProperty("type")]
        public string SlackEventType { get; set; }
    }
}