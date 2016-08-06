using System;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using Theorem.Converters;
using Theorem.Models.Events;

namespace Theorem.Models.Events
{
    public class MessageEventModel : EventModel
    {
        [JsonProperty("text")]
        public string Text { get; set; }
        
        [JsonProperty("ts")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime TimeSent { get; set; }
        
        [JsonProperty("team")]
        public string TeamId { get; set; }
    }
}