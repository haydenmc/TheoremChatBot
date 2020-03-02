using System;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using Theorem.Converters;

namespace Theorem.Models.Slack.Events
{
    public class SlackChannelCreatedEventModel : SlackEventModel
    {
        [NotMapped]
        [JsonProperty("channel")]
        public CreatedChannelModel CreatedChannel { get; set; }

        public class CreatedChannelModel
        {
            [JsonProperty("id")]
            public string SlackId { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("created")]
            [JsonConverter(typeof(UnixDateTimeConverter))]
            public DateTime Created { get; set; }

            [JsonProperty("creator")]
            public string CreatorSlackId { get; set; }
        }
    }
}