using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Theorem.ChatServices;
using Theorem.Converters;

namespace Theorem.Models.Mattermost.EventData
{
    public class MattermostPostedEventPostMetadataDataModel
    {
        // TODO
    }

    public class MattermostPostedEventPostDataModel
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("create_at")]
        public ulong CreateAt { get; set; }

        [JsonProperty("update_at")]
        public ulong UpdateAt { get; set; }

        [JsonProperty("edit_at")]
        public ulong EditAt { get; set; }

        [JsonProperty("delete_at")]
        public ulong DeleteAt { get; set; }

        [JsonProperty("is_pinned")]
        public bool IsPinned { get; set; }

        [JsonProperty("user_id")]
        public string UserId { get; set; }

        [JsonProperty("channel_id")]
        public string ChannelId { get; set; }

        [JsonProperty("root_id")]
        public string RootId { get; set; }

        [JsonProperty("parent_id")]
        public string ParentId { get; set; }

        [JsonProperty("original_id")]
        public string OriginalId { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
        
        [JsonProperty("type")]
        public string Type { get; set; }

        // Unclear what "props" are - this is where that'd be

        [JsonProperty("hashtags")]
        public string Hashtags { get; set; }

        [JsonProperty("pending_post_id")]
        public string PendingPostId { get; set; }

        [JsonProperty("metadata")]
        public MattermostPostedEventPostMetadataDataModel Metadata { get; set; }
    }

    public class MattermostPostedEventDataModel : 
        IProvideChatMessageModel
    {
        [JsonProperty("channel_display_name")]
        public string ChannelDisplayName { get; set; }

        [JsonProperty("channel_name")]
        public string ChannelName { get; set; }

        [JsonProperty("channel_type")]
        public string ChannelType { get; set; }

        [JsonProperty("mentions")]
        [JsonConverter(typeof(NestedJsonStringConverter))]
        public IEnumerable<string> Mentions { get; set; }

        [JsonProperty("post")]
        [JsonConverter(typeof(NestedJsonStringConverter))]
        public MattermostPostedEventPostDataModel Post { get; set; }

        [JsonProperty("sender_name")]
        public string SenderName { get; set; }

        [JsonProperty("team_id")]
        public string TeamId { get; set; }

        public ChatMessageModel ToChatMessageModel(IChatServiceConnection chatServiceConnection)
        {
            // if we receive a message in a channel with only one other member, we assume it's a private message
            // TODO: figure out better way to detect private messages
            var getChannelMemberCountTask = chatServiceConnection.GetMemberCountFromChannelIdAsync(Post.ChannelId);
            getChannelMemberCountTask.Wait();
            var channelMemberCount = getChannelMemberCountTask.Result;

            return new ChatMessageModel()
            {
                Id = Post.Id,
                Provider = ChatServiceKind.Mattermost,
                ProviderInstance = chatServiceConnection.Name,
                AuthorId = Post.UserId,
                Body = Post.Message,
                ChannelId = Post.ChannelId,
                TimeSent = DateTimeOffset.FromUnixTimeMilliseconds((long)Post.CreateAt),
                ThreadingId = Post.ParentId,
                FromChatServiceConnection = chatServiceConnection,
                IsFromTheorem = (Post.UserId == chatServiceConnection.UserId),
                IsMentioningTheorem = 
                    ((Mentions == null) ?
                        false :
                        Mentions.Contains(chatServiceConnection.UserId)),
                IsPrivateMessage = channelMemberCount == 2
            };
        }
    }
}