using System;
using Newtonsoft.Json;
using Theorem.Converters;

namespace Theorem.Models
{
    public class MessageEventModel : SlackEventModel
    {
        [JsonProperty("channel")]
        public string ChannelId { get; set; }
        
        [JsonProperty("user")]
        public string UserId { get; set; }
        
        [JsonProperty("text")]
        public string Text { get; set; }
        
        [JsonProperty("ts")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime TimeSent { get; set; }
        
        [JsonProperty("team")]
        public string TeamId { get; set; }
    }
}