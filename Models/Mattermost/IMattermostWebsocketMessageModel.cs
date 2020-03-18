namespace Theorem.Models.Mattermost
{
    public interface IMattermostWebsocketMessageModel
    {
        int Seq { get; }

        string Event { get; }

        string Action { get; }

        object Data { get; }

        MattermostEventBroadcastModel Broadcast { get; set; }
    }
}