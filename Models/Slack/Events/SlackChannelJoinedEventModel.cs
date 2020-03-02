using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Theorem.Models.Slack.Events
{
    public class SlackChannelJoinedEventModel : SlackEventModel
    {
        [NotMapped]
        [JsonProperty("channel")]
        public SlackChannelModel SlackChannel { get; set; }
    }
}