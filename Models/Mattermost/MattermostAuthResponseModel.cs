using Newtonsoft.Json;

namespace Theorem.Models.Mattermost
{
    public class MattermostAuthResponseModel
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("seq_reply")]
        public string SeqReply { get; set; }
    }
}