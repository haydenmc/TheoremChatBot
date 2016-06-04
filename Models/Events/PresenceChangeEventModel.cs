using System;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Theorem.Models.Events
{
    public class PresenceChangeEventModel : EventModel
    {
        // [ForeignKey("UserId")]
        // public new UserModel User { get; set; }
        
        [NotMapped]
        [JsonProperty("user")]
        public string SlackUserId { get; set; }
        
        [JsonProperty("presence")]
        public string Presence { get; set;}
    }
}