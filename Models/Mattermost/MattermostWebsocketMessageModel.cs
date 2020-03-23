using Newtonsoft.Json;

namespace Theorem.Models.Mattermost
{
    public class MattermostWebsocketMessageModel<T> : 
        IMattermostWebsocketMessageModel
    {
        [JsonProperty("seq")]
        public int Seq { get; set; }

        [JsonProperty("event")]
        public string Event { get; set; }

        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("data")]
        public T Data { get; set; }

        [JsonIgnore]
        object IMattermostWebsocketMessageModel.Data => Data;

        [JsonProperty("broadcast")]
        public MattermostEventBroadcastModel Broadcast { get; set; }

    }
}