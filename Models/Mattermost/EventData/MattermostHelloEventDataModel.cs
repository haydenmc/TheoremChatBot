using Newtonsoft.Json;

namespace Theorem.Models.Mattermost.EventData
{
    public class MattermostHelloEventDataModel
    {
        [JsonProperty("server_version")]
        public string ServerVersion { get; set; }
    }
}