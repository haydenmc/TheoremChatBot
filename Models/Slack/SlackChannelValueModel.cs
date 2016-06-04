using System;
using Newtonsoft.Json;
using Theorem.Converters;

namespace Theorem.Models.Slack
{
    public class SlackChannelValueModel
    {
        [JsonProperty("value")]
        public string Value { get; set; }
        
        [JsonProperty("creator")]
        public string CreatorId { get; set; }
        
        [JsonProperty("last_set")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime TimeLastSet { get; set; }
    }
}