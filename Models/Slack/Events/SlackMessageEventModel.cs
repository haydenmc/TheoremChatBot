using System;
using Newtonsoft.Json;
using Theorem.ChatServices;

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

        public ChatMessageModel ToChatMessageModel(IChatServiceConnection chatServiceConnection)
        {
            return new ChatMessageModel()
            {
                Id = SlackTimeSent,
                Provider = ChatServiceKind.Slack,
                ProviderInstance = chatServiceConnection.Name,
                AuthorId = SlackUserId,
                Body = Text,
                ChannelId = SlackChannelId,
                TimeSent = DateTimeOffset.FromUnixTimeSeconds(
                    long.Parse(SlackTimeSent.Split(".")[0])),
                ThreadingId = SlackThreadId,
                FromChatServiceConnection = chatServiceConnection,
                IsFromTheorem = (SlackUserId == chatServiceConnection.UserId),
                // This logic may be a little too rudimentary to handle all edge cases,
                // but it's fine for now:
                IsMentioningTheorem = Text.Contains($"<@{chatServiceConnection.UserId}>"),
                IsPrivateMessage = string.IsNullOrEmpty(SlackChannelId)
            };
        }
    }
}