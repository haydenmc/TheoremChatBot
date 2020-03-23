using Theorem.ChatServices;

namespace Theorem.Models
{
    public interface IProvideChatMessageModel
    {
        ChatMessageModel ToChatMessageModel(IChatServiceConnection chatServiceConnection);
    }
}