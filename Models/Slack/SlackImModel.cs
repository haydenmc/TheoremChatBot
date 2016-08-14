using System;
using Newtonsoft.Json;
using Theorem.Converters;

namespace Theorem.Models.Slack
{
    public class SlackImModel
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("is_im")]
        public bool IsIm { get; set; }

        [JsonProperty("user")]
        public string UserId { get; set; }

        [JsonProperty("created")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime TimeCreated { get; set; }

        [JsonProperty("is_user_deleted")]
        public bool IsUserDeleted { get; set; }
    }
}