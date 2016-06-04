using System.Collections.Generic;
using Newtonsoft.Json;

namespace Theorem.Models.Slack
{
    public class SlackStartResponseModel
    {
        [JsonProperty("ok")]
        public bool Ok { get; set; }
        
        [JsonProperty("url")]
        public string Url { get; set; }
        
        [JsonProperty("self")]
        public SlackSelfModel Self { get; set; }
        
        [JsonProperty("users")]
        public List<SlackUserModel> Users { get; set; }
        
        [JsonProperty("channels")]
        public List<SlackChannelModel> Channels { get; set; }
    }
}