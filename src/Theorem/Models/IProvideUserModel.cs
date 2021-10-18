using Theorem.ChatServices;

namespace Theorem.Models
{
    public interface IProvideUserModel
    {
        UserModel ToUserModel(IChatServiceConnection chatServiceConnection);
    }
}