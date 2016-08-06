using System;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Theorem.Models.Events
{
    public class EventModel
    {
        public Guid Id { get; set; }
        
        [JsonProperty("type")]
        public string SlackEventType { get; set; }
        
        [NotMapped]
        [JsonProperty("user")]
        public string SlackUserId { get; set; }

        [ForeignKey("UserId")]
        public UserModel User { get; set; }
        public Guid? UserId { get; set; }
        
        [NotMapped]
        [JsonProperty("channel")]
        public string SlackChannelId { get; set; }
        
        [ForeignKey("ChannelId")]
        public ChannelModel Channel { get; set; }
        public Guid? ChannelId { get; set; }

        [JsonIgnore]
        public DateTimeOffset TimeReceived { get; set; }
    }
}