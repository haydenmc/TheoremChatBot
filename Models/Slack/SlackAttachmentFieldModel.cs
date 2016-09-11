using Newtonsoft.Json;

namespace Theorem.Models.Slack
{
    public class SlackAttachmentFieldModel
    {
        [JsonProperty("title")]
        public string Title { get; set; }
        [JsonProperty("value")]
        public string Value { get; set; }
        [JsonProperty("short")]
        public bool Short { get; set; }
    }
}