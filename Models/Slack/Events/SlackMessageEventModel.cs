using System;
using Newtonsoft.Json;

namespace Theorem.Models.Slack.Events
{
    public class SlackMessageEventModel : 
        SlackEventModel,
        IProvideChatMessageModel
    {
        [JsonProperty("text")]
        public string Text { get; set; }
        
        [JsonProperty("ts")]
        public string SlackTimeSent { get; set; }
        
        [JsonProperty("thread_ts")]
        public string SlackThreadId { get; set; }
        
        [JsonProperty("team")]
        public string SlackTeamId { get; set; }

        public ChatMessageModel ToChatMessageModel()
        {
            return new ChatMessageModel()
            {
                Id = SlackTimeSent,
                Provider = ProviderKind.Slack,
                AuthorId = SlackUserId,
                Body = Text,
                ChannelId = SlackChannelId,
                TimeSent = DateTimeOffset.FromUnixTimeSeconds(
                    long.Parse(SlackTimeSent.Split(".")[0])),
                ThreadingId = SlackThreadId
            };
        }
    }
}