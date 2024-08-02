using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Theorem.Models.Matrix
{

    public class MatrixRoomMessageEventContentJsonConverter :
        JsonConverter<MatrixRoomMessageEventContent>
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsAssignableFrom(typeof(MatrixRoomMessageEventContent));
        }

        public override MatrixRoomMessageEventContent Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (JsonDocument.TryParseValue(ref reader, out var doc))
            {
                if (doc.RootElement.TryGetProperty("msgtype", out var type))
                {
                    var typeValue = type.GetString();
                    var rootElement = doc.RootElement.GetRawText();

                    return typeValue switch
                    {
                        "m.text" => JsonSerializer
                            .Deserialize<MatrixRoomMessageEventTextContent>(rootElement, options),
                        "m.image" => JsonSerializer
                            .Deserialize<MatrixRoomMessageEventImageContent>(rootElement, options),
                        _ => new MatrixRoomMessageEventContent() { MessageType = typeValue },
                    };
                }
                // TODO: Probably a better way to handle this, but this workaround
                // Handles empty "content" fields in case of redacted messages.
                return new MatrixRoomMessageEventContent() { MessageType = "" };
                // throw new JsonException(
                //     "Failed to extract type property from MatrixRoomMessageEventContent");
            }
            throw new JsonException("Failed to parse MatrixRoomMessageEventContent");
        }

        public override void Write(Utf8JsonWriter writer, MatrixRoomMessageEventContent value,
            JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }

    public record MatrixRoomMessageEvent : MatrixRoomEvent
    {
        [JsonPropertyName("content")]
        public MatrixRoomMessageEventContent Content { get; init; }
    }

    [JsonConverter(typeof(MatrixRoomMessageEventContentJsonConverter))]
    public record MatrixRoomMessageEventContent
    {
        [JsonPropertyName("msgtype")]
        public string MessageType { get; init; }
    }

    public record MatrixRoomMessageEventTextContent : MatrixRoomMessageEventContent
    {
        [JsonPropertyName("body")]
        public string Body { get; init; }

        [JsonPropertyName("format")]
        public string Format { get; init; }

        [JsonPropertyName("formatted_body")]
        public string FormattedBody { get; init; }
    }

    public record MatrixRoomMessageEventImageContent : MatrixRoomMessageEventContent
    {
        [JsonPropertyName("body")]
        public string Body { get; init; }

        // "file" field

        [JsonPropertyName("url")]
        public string Url { get; init; }
    }
}