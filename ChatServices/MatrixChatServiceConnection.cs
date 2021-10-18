using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
using Theorem.Models.Matrix;

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

        public string UserName
        {
            get
            {
                return _username;
            }
        }

        public ICollection<ChannelModel> Channels
        {
            get
            {
                return _channels;
            }
        }

        public bool IsConnected { get; private set; } = false;

        public event EventHandler<EventArgs> Connected;

        public event EventHandler<ChatMessageModel> MessageReceived;

        public event EventHandler<ICollection<ChannelModel>> ChannelsUpdated;

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

        private List<ChannelModel> _channels = new List<ChannelModel>();

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

        public async Task<string> SendMessageToChannelIdAsync(string channelId,
            ChatMessageModel message)
        {
            var eventResponse = await sendTextMessageToRoomAsync(channelId, message.Body);
            return eventResponse.EventId;
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

        private void updateChannels(MatrixSyncResponse syncResponse)
        {
            if (syncResponse.Rooms?.JoinedRooms != null)
            {
                
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

        private async Task<MatrixSendResponse> sendTextMessageToRoomAsync(string roomId,
            string body)
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
            var eventHandler = MessageReceived;
            if (eventHandler != null)
            {
                eventHandler(this, message);
            }
        }

        protected virtual void onChannelsUpdated()
        {
            var eventHandler = ChannelsUpdated;
            if (eventHandler != null)
            {
                // Create a shallow copy of our list
                var channelList = _channels.ToList();
                eventHandler(this, channelList);
            }
        }
    }
}