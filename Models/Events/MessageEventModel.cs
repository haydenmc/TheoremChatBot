using System;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using Theorem.Converters;
using Theorem.Models.Events;

namespace Theorem.Models
{
    public class MessageEventModel : EventModel
    {
        [NotMapped]
        [JsonProperty("channel")]
        public string SlackChannelId { get; set; }
        
        // [ForeignKey("ChannelId")]
        // [InverseProperty("Messages")]
        // public new ChannelModel Channel { get; set; }
        
        [NotMapped]
        [JsonProperty("user")]
        public string SlackUserId { get; set; }
        
        // [ForeignKey("UserId")]
        // [InverseProperty("Messages")]
        // public new UserModel User { get; set; }
        
        [JsonProperty("text")]
        public string Text { get; set; }
        
        [JsonProperty("ts")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime TimeSent { get; set; }
        
        [JsonProperty("team")]
        public string TeamId { get; set; }
    }
}