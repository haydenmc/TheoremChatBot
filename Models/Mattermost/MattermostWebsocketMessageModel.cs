using Newtonsoft.Json;

namespace Theorem.Models.Mattermost
{
    public class MattermostWebsocketMessageModel<T>
    {
        [JsonProperty("seq")]
        public int Seq { get; set; }

        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("data")]
        public T Data { get; set; }
    }
}