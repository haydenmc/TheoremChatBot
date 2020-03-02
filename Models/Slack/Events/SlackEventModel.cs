using System;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Theorem.Models.Slack.Events
{
    public class SlackEventModel
    {
        [JsonIgnore]
        public Guid Id { get; set; }
        
        [JsonProperty("type")]
        public string SlackEventType { get; set; }
        
        [NotMapped]
        [JsonProperty("user")]
        public string SlackUserId { get; set; }

        [JsonIgnore]
        [ForeignKey("UserId")]
        public UserModel User { get; set; }

        [JsonIgnore]
        public Guid? UserId { get; set; }
        
        [NotMapped]
        [JsonProperty("channel")]
        public string SlackChannelId { get; set; }
        
        [JsonIgnore]
        [ForeignKey("ChannelId")]
        public ChannelModel Channel { get; set; }

        [JsonIgnore]
        public Guid? ChannelId { get; set; }

        [JsonIgnore]
        public DateTimeOffset TimeReceived { get; set; }
    }
}