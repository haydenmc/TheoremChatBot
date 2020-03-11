namespace Theorem.Models
{
    public enum ProviderKind
    {
        Slack,
        Mattermost
    }

    public abstract class ChatMessageModel
    {
        public virtual ProviderKind Provider { get; }
        public virtual string ProviderInstance { get; }
        public virtual string AuthorId { get; }
        public virtual string Body { get; }
        public virtual string ChannelId { get; }
    }
}