using System;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using Theorem.Converters;
using Theorem.Models.Events;
using Theorem.Models.Slack;

namespace Theorem.Models.Events
{
    public class ChannelJoinedEventModel : EventModel
    {
        [NotMapped]
        [JsonProperty("channel")]
        public SlackChannelModel SlackChannel { get; set; }
    }
}