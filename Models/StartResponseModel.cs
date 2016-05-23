using Newtonsoft.Json;

namespace Theorem.Models
{
    public class StartResponseModel
    {
        [JsonProperty("ok")]
        public bool Ok { get; set; }
        
        [JsonProperty("url")]
        public string Url { get; set; }
    }
}