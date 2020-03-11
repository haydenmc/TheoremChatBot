using System;
using System.Threading.Tasks;
using Theorem.Models;

namespace Theorem.Providers
{
    public interface IChatProvider
    {
        event EventHandler<EventArgs> Connected;

        event EventHandler<ChatMessageModel> NewMessage;
        
        Task Connect();

        Task SendMessageToChannelId(string channelId, string body);
    }
}