using System;
using System.Threading.Tasks;
using Theorem.Models.Events;

namespace Theorem.Providers
{
    public interface IChatProvider
    {
        event EventHandler<EventArgs> Connected;

        event EventHandler<MessageEventModel> NewMessage;
        
        Task Connect();

        Task SendMessageToChannelId(string channelId, string body);
    }
}