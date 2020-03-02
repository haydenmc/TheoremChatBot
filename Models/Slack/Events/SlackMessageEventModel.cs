using Newtonsoft.Json;

namespace Theorem.Models.Slack.Events
{
    public class SlackMessageEventModel : 
        SlackEventModel,
        IChatMessageModel
    {
        [JsonProperty("text")]
        public string Text { get; set; }
        
        [JsonProperty("ts")]
        public decimal SlackTimeSent { get; set; }
        
        [JsonProperty("thread_ts")]
        public decimal SlackThreadId { get; set; }
        
        [JsonProperty("team")]
        public string SlackTeamId { get; set; }

        /* IChatMessageModel */
        [JsonIgnore]
        public string AuthorId => User.SlackId;

        [JsonIgnore]
        public string Body => Text;

        [JsonIgnore]
        string IChatMessageModel.ChannelId => Channel.SlackId;
    }
}