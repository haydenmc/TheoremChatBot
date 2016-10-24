using System;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using Theorem.Converters;
using Theorem.Models.Events;
using Theorem.Models.Slack;

namespace Theorem.Models.Events
{
    public class ChannelCreatedEventModel : EventModel
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