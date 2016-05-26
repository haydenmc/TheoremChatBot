using System.Collections.Generic;
using Newtonsoft.Json;

namespace Theorem.Models
{
    public class StartResponseModel
    {
        [JsonProperty("ok")]
        public bool Ok { get; set; }
        
        [JsonProperty("url")]
        public string Url { get; set; }
        
        [JsonProperty("self")]
        public SelfModel Self { get; set; }
        
        [JsonProperty("users")]
        public List<UserModel> Users { get; set; }
        
        [JsonProperty("channels")]
        public List<ChannelModel> Channels { get; set; }
    }
}