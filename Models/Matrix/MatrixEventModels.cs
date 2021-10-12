using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Theorem.Models.Matrix
{
    public class MatrixEventJsonConverter : JsonConverter<MatrixEvent>
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsAssignableFrom(typeof(MatrixEvent));
        }

        public override MatrixEvent Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (JsonDocument.TryParseValue(ref reader, out var doc))
            {
                if (doc.RootElement.TryGetProperty("type", out var type))
                {
                    var typeValue = type.GetString();
                    var rootElement = doc.RootElement.GetRawText();

                    return typeValue switch
                    {
                        "m.room.message" => JsonSerializer
                            .Deserialize<MatrixRoomMessageEvent>(rootElement, options),
                        "m.room.member" => JsonSerializer
                            .Deserialize<MatrixRoomMemberEvent>(rootElement, options),
                        _ => new MatrixEvent() { Type = typeValue },
                    };
                }
                throw new JsonException("Failed to extract type property from MatrixEvent");
            }
            throw new JsonException("Failed to parse MatrixEvent");
        }

        public override void Write(Utf8JsonWriter writer, MatrixEvent value,
            JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }

    [JsonConverter(typeof(MatrixEventJsonConverter))]
    public record MatrixEvent
    {
        // https://matrix.org/docs/spec/client_server/latest#id244

        [JsonPropertyName("type")]
        public string Type { get; init; }
    }

    public record MatrixRoomEvent : MatrixEvent
    {
        // m.room
        // https://matrix.org/docs/spec/client_server/latest#id245

        [JsonPropertyName("event_id")]
        public string EventId { get; init; }

        [JsonPropertyName("sender")]
        public string Sender { get; init; }

        [JsonPropertyName("origin_server_ts")]
        public UInt64 OriginServerTimestamp { get; init; }

        // TODO: Unsigned data

        [JsonPropertyName("room_id")]
        public string RoomId { get; init; }
    }

    public record MatrixRoomMessageEvent : MatrixRoomEvent
    {
        [JsonPropertyName("content")]
        public MatrixRoomMessageEventContent Content { get; init; }
    }

    public record MatrixRoomMessageEventContent
    {
        [JsonPropertyName("body")]
        public string Body { get; init; }

        [JsonPropertyName("msgtype")]
        public string MessageType { get; init; }
    }

    public record MatrixRoomMemberEvent : MatrixRoomEvent
    {
        [JsonPropertyName("content")]
        public MatrixRoomMemberEventContent Content { get; init; }
        
        [JsonPropertyName("state_key")]
        public string StateKey { get; init; }
    }

    public record MatrixRoomMemberEventContent
    {
        // https://matrix.org/docs/spec/client_server/latest#id252

        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; init; }

        [JsonPropertyName("displayname")]
        public string DisplayName { get; init; }

        [JsonPropertyName("membership")]
        public string Membership { get; init; }

        [JsonPropertyName("is_direct")]
        public bool IsDirect { get; init; }

        // Additional fields 
    }
}