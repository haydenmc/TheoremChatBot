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
        public decimal SlackTimeSent { get; set; }
        
        [JsonProperty("thread_ts")]
        public decimal SlackThreadId { get; set; }
        
        [JsonProperty("team")]
        public string SlackTeamId { get; set; }
    }
}