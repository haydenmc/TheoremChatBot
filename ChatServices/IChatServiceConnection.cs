using System;
using System.Threading.Tasks;
using Theorem.Models;

namespace Theorem.ChatServices
{
    public interface IChatServiceConnection
    {
        event EventHandler<EventArgs> Connected;

        event EventHandler<ChatMessageModel> NewMessage;
        
        Task Connect();

        Task SendMessageToChannelId(string channelId, string body);
    }
}