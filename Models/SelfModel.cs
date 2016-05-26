using Newtonsoft.Json;

namespace Theorem.Models
{
    public class SelfModel
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        
        [JsonProperty("name")]
        public string Name { get; set; }
    }
}