using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Theorem.Models;

namespace Theorem.ChatServices
{
    public interface IChatServiceConnection
    {
        string Name { get; }

        string UserId { get; }

        string UserName { get; }

        ICollection<ChannelModel> Channels { get; }

        bool IsConnected { get; }

        event EventHandler<EventArgs> Connected;

        event EventHandler<ChatMessageModel> MessageReceived;

        event EventHandler<ICollection<ChannelModel>> ChannelsUpdated;
        
        Task StartAsync();

        Task<string> SendMessageToChannelIdAsync(string channelId, ChatMessageModel message);

        Task<string> SendMessageReactionAsync(string channelId, string messageId,
            string unicodeReaction);

        Task<string> UpdateMessageAsync(string channelId, string messageId,
            ChatMessageModel message);

        Task<string> GetChannelIdFromChannelNameAsync(string channelName);

        Task SetChannelTopicAsync(string channelId, string topic);

        Task<IEnumerable<ReactionModel>> GetMessageReactionsAsync(string channelId,
            string messageId);
    }
}