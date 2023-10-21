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
                        "m.reaction" => JsonSerializer
                            .Deserialize<MatrixRoomReactionEvent>(rootElement, options),
                        "m.room.canonical_alias" => JsonSerializer
                            .Deserialize<MatrixRoomCanonicalAliasEvent>(rootElement, options),
                        "m.room.create" => JsonSerializer
                            .Deserialize<MatrixRoomCreateEvent>(rootElement, options),
                        "m.room.member" => JsonSerializer
                            .Deserialize<MatrixRoomMemberEvent>(rootElement, options),
                        "m.room.message" => JsonSerializer
                            .Deserialize<MatrixRoomMessageEvent>(rootElement, options),
                        "m.room.name" => JsonSerializer
                            .Deserialize<MatrixRoomNameEvent>(rootElement, options),
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

    public record MatrixRoomCanonicalAliasEvent : MatrixRoomEvent
    {
        [JsonPropertyName("content")]
        public MatrixRoomCanonicalAliasEventContent Content { get; init; }
    }

    public record MatrixRoomCanonicalAliasEventContent
    {
        [JsonPropertyName("alias")]
        public string RoomAlias { get; init; }

        [JsonPropertyName("alt_aliases")]
        public string[] AlternateRoomAliases { get; init; }
    }

    public record MatrixRoomCreateEvent : MatrixRoomEvent
    {
        [JsonPropertyName("content")]
        public MatrixRoomCreateEventContent Content { get; init; }
    }

    public record MatrixRoomCreateEventContent
    {
        [JsonPropertyName("creator")]
        public string CreatorId { get; init; }

        [JsonPropertyName("m.federate")]
        public bool CanFederate { get; init; }

        [JsonPropertyName("room_version")]
        public string RoomVersion { get; init; }

        [JsonPropertyName("predecessor")]
        public MatrixPreviousRoom PreviousRoom { get; init; }
    }

    public record MatrixPreviousRoom
    {
        [JsonPropertyName("room_id")]
        public string RoomId { get; init; }

        [JsonPropertyName("event_id")]
        public string LastRoomEventId { get; init; }
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

    public record MatrixRoomNameEvent : MatrixRoomEvent
    {
        [JsonPropertyName("content")]
        public MatrixRoomNameEventContent Content { get; init; }
    }

    public record MatrixRoomNameEventContent
    {
        [JsonPropertyName("name")]
        public string Name { get; init; }
    }

    public record MatrixRoomReactionEvent : MatrixRoomEvent
    {
        [JsonPropertyName("content")]
        public MatrixRoomEventRelatesToContent Content { get; init; }
    }

    public record MatrixRoomEventRelatesToContent
    {
        [JsonPropertyName("m.relates_to")]
        public MatrixRelatesToModel RelatesTo { get; init; }
    }

    public record MatrixRelatesToModel
    {
        [JsonPropertyName("event_id")]
        public string EventId { get; init; }

        [JsonPropertyName("key")]
        public string Key { get; init; }

        [JsonPropertyName("rel_type")]
        public string RelationType { get; init; }
    }
}