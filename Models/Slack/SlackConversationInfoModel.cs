using System.Collections.Generic;
using Newtonsoft.Json;

namespace Theorem.Models.Slack
{
    public class SlackConversationInfoModel
    {
        [JsonProperty("ok")]
        public bool Ok { get; set; }
        
        [JsonProperty("channel")]
        public SlackChannelModel Channel { get; set; }
    }
}