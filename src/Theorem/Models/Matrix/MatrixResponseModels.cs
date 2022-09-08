using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Theorem.Models.Matrix
{
    public record MatrixError
    {
        [JsonPropertyName("errcode")]
        public string ErrorCode { get; init; }

        [JsonPropertyName("error")]
        public string Error { get; init; }
    }

    public record MatrixLoginResponse
    {
        [JsonPropertyName("user_id")]
        public string UserId { get; init; }

        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; }

        [JsonPropertyName("device_id")]
        public string DeviceId { get; init; }

        // additional fields @ https://matrix.org/docs/spec/client_server/latest#id205
    }

    public record MatrixJoinRoomResponse
    {
        [JsonPropertyName("room_id")]
        public string RoomId { get; init; }
    }

    public record MatrixSendResponse
    {
        [JsonPropertyName("event_id")]
        public string EventId { get; init; }
    }

    public record MatrixDirectoryRoomResponse
    {
        [JsonPropertyName("room_id")]
        public string RoomId { get; init; }

        // Additional fields @ https://matrix.org/docs/spec/client_server/latest#id282
    }

    public record MatrixSyncResponse
    {
        [JsonPropertyName("next_batch")]
        public string NextBatchToken { get; init; }

        [JsonPropertyName("rooms")]
        public MatrixSyncResponseRooms Rooms { get; init; }

        // Additional fields @ https://spec.matrix.org/v1.3/client-server-api/#get_matrixclientv3sync
    }

    public record MatrixSyncResponseRooms
    {
        [JsonPropertyName("join")]
        public Dictionary<string, MatrixJoinedRoom> JoinedRooms { get; init; }

        [JsonPropertyName("invite")]
        public Dictionary<string, MatrixInvitedRoom> InvitedRooms { get; init; }

        [JsonPropertyName("leave")]
        public Dictionary<string, MatrixLeftRoom> LeftRooms { get; init; }
    }

    public record MatrixJoinedRoom
    {
        [JsonPropertyName("state")]
        public MatrixState State { get; init; }

        [JsonPropertyName("timeline")]
        public MatrixTimeline Timeline { get; init; }

        // Additional fields https://matrix.org/docs/spec/client_server/latest#id257
    }

    public record MatrixState
    {
        [JsonPropertyName("events")]
        public IList<MatrixEvent> Events { get; init; }
    }

    public record MatrixTimeline
    {
        [JsonPropertyName("events")]
        public IList<MatrixEvent> Events { get; init; }

        [JsonPropertyName("limited")]
        public bool IsLimited { get; init; }

        [JsonPropertyName("prev_batch")]
        public string PreviousBatchToken { get; init; }
    }

    public record MatrixInvitedRoom
    {
        [JsonPropertyName("invite_state")]
        public MatrixInviteState InviteState { get; init; }
    }

    public record MatrixInviteState
    {
        [JsonPropertyName("events")]
        public IList<MatrixStrippedState> Events { get; init; }
    }

    public record MatrixLeftRoom
    {
        [JsonPropertyName("state")]
        public MatrixState State { get; init; }

        [JsonPropertyName("timeline")]
        public MatrixTimeline Timeline { get; init; }

        // Additional fields
    }

    public record MatrixStrippedState
    {
        [JsonPropertyName("content")]
        public dynamic EventContent { get; init; }

        [JsonPropertyName("state_key")]
        public string StateKey { get; init; }

        [JsonPropertyName("type")]
        public string Type { get; init; }

        [JsonPropertyName("sender")]
        public string Sender { get; init; }
    }
}