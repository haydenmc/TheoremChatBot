using System;
using Newtonsoft.Json;
using Theorem.Converters;
using Theorem.Models.Events;

namespace Theorem.Models
{
    public class MessageEventModel : EventModel
    {
        [JsonProperty("channel")]
        public string SlackChannelId { get; set; }
        
        [JsonProperty("user")]
        public string SlackUserId { get; set; }
        
        [JsonProperty("text")]
        public string Text { get; set; }
        
        [JsonProperty("ts")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime TimeSent { get; set; }
        
        [JsonProperty("team")]
        public string TeamId { get; set; }
    }
}