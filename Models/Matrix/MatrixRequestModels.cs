using System.Text.Json.Serialization;

namespace Theorem.Models.Matrix
{
    public record MatrixLoginPayload
    {
        [JsonPropertyName("type")]
        public string Type { get; init; }

        [JsonPropertyName("identifier")]
        public MatrixLoginIdentifier Identifier { get; init; }

        [JsonPropertyName("password")]
        public string Password { get; init; }

        [JsonPropertyName("device_id")]
        public string DeviceId { get; init; }

        [JsonPropertyName("initial_device_display_name")]
        public string InitialDeviceDisplayName { get; init; }
    }
    
    public record MatrixLoginIdentifier
    {
        [JsonPropertyName("type")]
        public string Type { get; init; }

        [JsonPropertyName("user")]
        public string User { get; init; }
    }
}