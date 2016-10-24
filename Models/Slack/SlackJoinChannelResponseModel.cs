using Newtonsoft.Json;

namespace Theorem.Models.Slack
{
    public class SlackJoinChannelResponseModel
    {
        [JsonProperty("ok")]
        public string Ok { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("already_in_channel")]
        public bool AlreadyInChannel { get; set; }

        [JsonProperty("channel")]
        public SlackChannelModel Channel { get; set; }
    }
}