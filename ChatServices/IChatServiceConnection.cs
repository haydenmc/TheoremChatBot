using System;
using System.Threading.Tasks;
using Theorem.Models;

namespace Theorem.ChatServices
{
    public interface IChatServiceConnection
    {
        string Name { get; }

        string UserId { get; }

        event EventHandler<EventArgs> Connected;

        event EventHandler<ChatMessageModel> NewMessage;
        
        Task StartAsync();

        Task SendMessageToChannelIdAsync(string channelId, string body);

        Task<string> GetChannelIdFromChannelNameAsync(string channelName);
    }
}