using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Theorem.Models;

namespace Theorem.ChatServices
{
    public interface IChatServiceConnection
    {
        string Name { get; }

        string UserId { get; }

        string UserName { get; }

        string MentionMessageRegExPrefix { get; }

        ObservableCollection<UserModel> Users { get; }

        ObservableCollection<UserModel> OnlineUsers { get; }

        event EventHandler<EventArgs> Connected;

        event EventHandler<ChatMessageModel> NewMessage;
        
        Task StartAsync();

        Task SendMessageToChannelIdAsync(string channelId, string body);

        Task<string> GetChannelIdFromChannelNameAsync(string channelName);

        Task<int> GetMemberCountFromChannelIdAsync(string channelId);

        Task SetChannelTopicAsync(string channelId, string topic);
    }
}