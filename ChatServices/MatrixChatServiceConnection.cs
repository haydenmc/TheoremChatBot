using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Theorem.Models;

namespace Theorem.ChatServices
{
    public class MatrixChatServiceConnection : IChatServiceConnection
    {
        /// <summary>
        /// Name of this connection instance as specified in configuration
        /// </summary>
        public string Name
        {
            get
            {
                return _configuration.Key;
            }
        }

        public string UserId
        {
            get
            {
                return _userId;
            }
        }

        public string UserName => "";

        public ObservableCollection<UserModel> Users => new ObservableCollection<UserModel>();

        public ObservableCollection<UserModel> OnlineUsers => new ObservableCollection<UserModel>();

        public bool IsConnected { get; private set; } = false;

        public event EventHandler<EventArgs> Connected;
        public event EventHandler<ChatMessageModel> NewMessage;

        private const int POLLING_TIMEOUT_MS = 30000;

        /// <summary>
        /// Configuration object for retrieving configuration values
        /// </summary>
        private ConfigurationSection _configuration { get; set; }

        /// <summary>
        /// Logger instance for logging events
        /// </summary>
        private ILogger<MatrixChatServiceConnection> _logger { get; set; }

        private Uri _serverBaseUrl;

        private string _username;

        private string _password;

        private string _deviceId;

        private string _deviceDisplayName;

        private string _accessToken;

        private string _userId;

        private string _nextSyncBatchToken;

        private string _roomServerRestriction;

        private int _nextTxnId = 0;

        private HttpClient _httpClient;

        public MatrixChatServiceConnection(ConfigurationSection configuration,
            ILogger<MatrixChatServiceConnection> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = new HttpClient();
            loadConfiguration();
            _httpClient.BaseAddress = _serverBaseUrl;
            _httpClient.DefaultRequestHeaders.Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task StartAsync()
        {
            _logger.LogInformation($"{Name}: Connecting to {_serverBaseUrl.ToString()}...");
            await loginAsync();
            _logger.LogInformation($"{Name}: Logged in as '{_userId}' on device '{_deviceId}'!");

            _logger.LogInformation($"{Name}: Running initial sync...");
            await initialSyncAsync();

            IsConnected = true;
            onConnected();

            while (true)
            {
                await pollSyncAsync();
            }
        }

        public async Task<string> GetChannelIdFromChannelNameAsync(string channelName)
        {
            // https://matrix.org/docs/spec/client_server/latest#id282
            var result = await _httpClient.GetAsync(
                $"/_matrix/client/r0/directory/room/{HttpUtility.UrlEncode(channelName)}");

            if (result.StatusCode != HttpStatusCode.OK)
            {
                var error = await result.Content.ReadFromJsonAsync<MatrixError>();
                throw new ApplicationException($"Received error on looking up Matrix room " +
                    $"{error.ErrorCode}: {error.Error}");
            }
            var response = await result.Content.ReadFromJsonAsync<MatrixDirectoryRoomResponse>();
            return response.RoomId;
        }

        public Task<int> GetMemberCountFromChannelIdAsync(string channelId)
        {
            // TODO
            return Task.FromResult<int>(0);
        }

        public async Task SendMessageToChannelIdAsync(string channelId, ChatMessageModel message)
        {
            await sendTextMessageToRoomAsync(channelId, message.Body);
        }

        public Task SetChannelTopicAsync(string channelId, string topic)
        {
            // TODO
            return new Task(() => {});
        }

        private void loadConfiguration()
        {
            _serverBaseUrl =
                _configuration.GetValue<Uri>("BaseUrl", new Uri("http://localhost:8008"));
            _username = _configuration.GetValue<string>("UserName", "theorem");
            _password = _configuration.GetValue<string>("Password", "");
            _deviceId = _configuration.GetValue<string>("DeviceId", "bot");
            _deviceDisplayName = _configuration.GetValue<string>("DeviceDisplayName", "Bot");
            _roomServerRestriction = _configuration.GetValue<string>("RoomServerRestriction", "");
        }

        private async Task loginAsync()
        {
            // https://matrix.org/docs/spec/client_server/latest#id205
            var postPayload = new MatrixLoginPayload()
            {
                Type = "m.login.password",
                Identifier = new MatrixLoginIdentifier()
                {
                    Type = "m.id.user",
                    User = _username,
                },
                Password = _password,
                DeviceId = _deviceId,
                InitialDeviceDisplayName = _deviceDisplayName,
            };
            var postPayloadString = JsonSerializer.Serialize(postPayload);
            var postContent = new StringContent(
                postPayloadString,
                Encoding.UTF8,
                "application/json");
            var result = await _httpClient.PostAsync("/_matrix/client/r0/login", postContent);
            if (result.StatusCode != HttpStatusCode.OK)
            {
                var error = await result.Content.ReadFromJsonAsync<MatrixError>();
                throw new ApplicationException($"Could not log in to Matrix: " + 
                    $"{error.ErrorCode}: {error.Error}");
            }

            var response = await result.Content.ReadFromJsonAsync<MatrixLoginResponse>();
            _deviceId = response.DeviceId;
            _accessToken = response.AccessToken;
            _userId = response.UserId;
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", response.AccessToken);
        }

        private async Task initialSyncAsync()
        {
            // https://matrix.org/docs/spec/client_server/latest#id257
            var result = await _httpClient.GetAsync("/_matrix/client/r0/sync");

            if (result.StatusCode != HttpStatusCode.OK)
            {
                var error = await result.Content.ReadFromJsonAsync<MatrixError>();
                throw new ApplicationException($"Received error on initial Matrix sync " +
                    $"{error.ErrorCode}: {error.Error}");
            }
            var response = await result.Content.ReadFromJsonAsync<MatrixSyncResponse>();
            _nextSyncBatchToken = response.NextBatchToken;
            await joinInvitedRoomsAsync(response);
        }

        private async Task joinInvitedRoomsAsync(MatrixSyncResponse syncResponse)
        {
            if (syncResponse.Rooms?.InvitedRooms != null)
            {
                foreach (var invitedRoom in syncResponse.Rooms.InvitedRooms)
                {
                    if ((_roomServerRestriction.Length > 0) && 
                        !(invitedRoom.Key.ToUpper().EndsWith(_roomServerRestriction.ToUpper())))
                    {
                        continue;
                    }
                    _logger.LogInformation($"Invited to room '{invitedRoom.Key}'");
                    await joinRoomIdAsync(invitedRoom.Key);
                }
            }
        }

        private async Task<MatrixJoinRoomResponse> joinRoomIdAsync(string roomId)
        {
            // https://matrix.org/docs/spec/client_server/latest#id290
            var result = await _httpClient.PostAsync(
                $"/_matrix/client/r0/rooms/{HttpUtility.UrlEncode(roomId)}/join", null);

            if (result.StatusCode != HttpStatusCode.OK)
            {
                var error = await result.Content.ReadFromJsonAsync<MatrixError>();
                throw new ApplicationException($"Error joining Matrix room " +
                    $"{error.ErrorCode}: {error.Error}");
            }
            var response = await result.Content.ReadFromJsonAsync<MatrixJoinRoomResponse>();
            _logger.LogInformation($"Joined room '{response.RoomId}'");
            return response;
        }

        private async Task pollSyncAsync()
        {
            // https://matrix.org/docs/spec/client_server/latest#id257
            var result = await _httpClient.GetAsync(
                $"/_matrix/client/r0/sync?since={HttpUtility.UrlEncode(_nextSyncBatchToken)}" + 
                $"&timeout={POLLING_TIMEOUT_MS}");

            if (result.StatusCode != HttpStatusCode.OK)
            {
                var error = await result.Content.ReadFromJsonAsync<MatrixError>();
                throw new ApplicationException($"Received error on polling Matrix sync " +
                    $"{error.ErrorCode}: {error.Error}");
            }
            var response = await result.Content.ReadFromJsonAsync<MatrixSyncResponse>();
            _nextSyncBatchToken = response.NextBatchToken;
            await joinInvitedRoomsAsync(response);
            processRoomMessages(response);
        }

        private void processRoomMessages(MatrixSyncResponse syncResponse)
        {
            if (syncResponse.Rooms?.JoinedRooms != null)
            {
                foreach (var room in syncResponse.Rooms.JoinedRooms)
                {
                    if (room.Value.Timeline?.Events != null)
                    {
                        foreach (var roomEvent in room.Value.Timeline.Events)
                        {
                            if (roomEvent is MatrixRoomMessageEvent)
                            {
                                processRoomMessage(room.Key, (MatrixRoomMessageEvent)roomEvent);
                            }
                        }
                    }
                }
            }
        }

        private void processRoomMessage(string roomId, MatrixRoomMessageEvent message)
        {
            var messageModel = new ChatMessageModel()
            {
                Id = message.EventId,
                Provider = ChatServiceKind.Matrix,
                ProviderInstance = Name,
                AuthorId = message.Sender,
                Body = message.Content?.Body,
                ChannelId = roomId,
                TimeSent = DateTimeOffset.FromUnixTimeMilliseconds(
                    (long)message.OriginServerTimestamp),
                ThreadingId = "",
                Attachments = null,
                FromChatServiceConnection = this,
                IsFromTheorem = (message.Sender.Equals(_userId)),
                IsMentioningTheorem = false,
            };
            onNewMessage(messageModel);
        }

        private async Task<MatrixSendResponse> sendTextMessageToRoomAsync(string roomId, string body)
        {
            // https://matrix.org/docs/spec/client_server/latest#id271
            var payload = new Dictionary<string, string>()
            {
                { "msgtype", "m.text" },
                { "body", body }
            };
            var payloadString = JsonSerializer.Serialize(payload);
            var content = new StringContent(payloadString, Encoding.UTF8, "application/json");

            var result = await _httpClient.PutAsync(
                $"/_matrix/client/r0/rooms/{HttpUtility.UrlEncode(roomId)}" +
                $"/send/m.room.message/{_nextTxnId++}", content);

            if (result.StatusCode != HttpStatusCode.OK)
            {
                var error = await result.Content.ReadFromJsonAsync<MatrixError>();
                throw new ApplicationException($"Received error sending room message " +
                    $"{error.ErrorCode}: {error.Error}");
            }
            var response = await result.Content.ReadFromJsonAsync<MatrixSendResponse>();
            return response;
        }

        private void onConnected()
        {
            var eventHandler = Connected;
            if (eventHandler != null)
            {
                eventHandler(this, EventArgs.Empty);
            }
        }

        private void onNewMessage(ChatMessageModel message)
        {
            var eventHandler = NewMessage;
            if (eventHandler != null)
            {
                eventHandler(this, message);
            }
        }

        private record MatrixError
        {
            [JsonPropertyName("errcode")]
            public string ErrorCode { get; init; }

            [JsonPropertyName("error")]
            public string Error { get; init; }
        }

        private record MatrixLoginIdentifier
        {
            [JsonPropertyName("type")]
            public string Type { get; init; }

            [JsonPropertyName("user")]
            public string User { get; init; }
        }

        private record MatrixLoginPayload
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

        private record MatrixLoginResponse
        {
            [JsonPropertyName("user_id")]
            public string UserId { get; init; }

            [JsonPropertyName("access_token")]
            public string AccessToken { get; init; }

            [JsonPropertyName("device_id")]
            public string DeviceId { get; init; }

            // additional fields @ https://matrix.org/docs/spec/client_server/latest#id205
        }

        private record MatrixJoinRoomResponse
        {
            [JsonPropertyName("room_id")]
            public string RoomId { get; init; }
        }

        private record MatrixSendResponse
        {
            [JsonPropertyName("event_id")]
            public string EventId { get; init; }
        }

        private record MatrixDirectoryRoomResponse
        {
            [JsonPropertyName("room_id")]
            public string RoomId { get; init; }

            // Additional fields @ https://matrix.org/docs/spec/client_server/latest#id282
        }

        private record MatrixSyncResponse
        {
            [JsonPropertyName("next_batch")]
            public string NextBatchToken { get; init; }

            [JsonPropertyName("rooms")]
            public MatrixSyncResponseRooms Rooms { get; init; }

            // Additional fields @ https://matrix.org/docs/spec/client_server/latest#id257
        }

        private record MatrixSyncResponseRooms
        {
            [JsonPropertyName("join")]
            public Dictionary<string, MatrixJoinedRoom> JoinedRooms { get; init; }

            [JsonPropertyName("invite")]
            public Dictionary<string, MatrixInvitedRoom> InvitedRooms { get; init; }

            [JsonPropertyName("leave")]
            public Dictionary<string, MatrixLeftRoom> LeftRooms { get; init; }
        }

        private record MatrixJoinedRoom
        {
            [JsonPropertyName("state")]
            public MatrixState State { get; init; }

            [JsonPropertyName("timeline")]
            public MatrixTimeline Timeline { get; init; }

            // Additional fields https://matrix.org/docs/spec/client_server/latest#id257
        }

        private record MatrixState
        {
            [JsonPropertyName("events")]
            public IList<MatrixEvent> Events { get; init; }
        }

        private record MatrixTimeline
        {
            [JsonPropertyName("events")]
            public IList<MatrixEvent> Events { get; init; }

            [JsonPropertyName("limited")]
            public bool IsLimited { get; init; }

            [JsonPropertyName("prev_batch")]
            public string PreviousBatchToken { get; init; }
        }

        private record MatrixInvitedRoom
        {
            [JsonPropertyName("invite_state")]
            public MatrixInviteState InviteState { get; init; }
        }

        private record MatrixInviteState
        {
            [JsonPropertyName("events")]
            public IList<MatrixStrippedState> Events { get; init; }
        }

        private record MatrixLeftRoom
        {
            [JsonPropertyName("state")]
            public MatrixState State { get; init; }

            [JsonPropertyName("timeline")]
            public MatrixTimeline Timeline { get; init; }

            // Additional fields
        }

        private record MatrixStrippedState
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

        [JsonConverter(typeof(MatrixEventJsonConverter))]
        private record MatrixEvent
        {
            // https://matrix.org/docs/spec/client_server/latest#id244

            [JsonPropertyName("type")]
            public string Type { get; init; }
        }

        private record MatrixRoomEvent : MatrixEvent
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

        private record MatrixRoomMessageEvent : MatrixRoomEvent
        {
            [JsonPropertyName("content")]
            public MatrixRoomMessageEventContent Content { get; init; }
        }

        private record MatrixRoomMessageEventContent
        {
            [JsonPropertyName("body")]
            public string Body { get; init; }

            [JsonPropertyName("msgtype")]
            public string MessageType { get; init; }
        }

        private record MatrixRoomMemberEvent : MatrixRoomEvent
        {
            [JsonPropertyName("content")]
            public MatrixRoomMemberEventContent Content { get; init; }
            
            [JsonPropertyName("state_key")]
            public string StateKey { get; init; }
        }

        private record MatrixRoomMemberEventContent
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

        private class MatrixEventJsonConverter : JsonConverter<MatrixEvent>
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
    }
}