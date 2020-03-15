using System;
using System.Threading.Tasks;
using Theorem.Models;

namespace Theorem.ChatServices
{
    public interface IChatServiceConnection
    {
        string Name { get; }

        event EventHandler<EventArgs> Connected;

        event EventHandler<ChatMessageModel> NewMessage;
        
        Task Connect();

        Task SendMessageToChannelId(string channelId, string body);
    }
}