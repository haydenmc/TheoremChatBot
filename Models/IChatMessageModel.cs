namespace Theorem.Models
{
    public interface IChatMessageModel
    {
        string AuthorId { get; }
        string Body { get; }
        string ChannelId { get; }
    }
}