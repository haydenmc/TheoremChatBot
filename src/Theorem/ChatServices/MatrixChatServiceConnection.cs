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
using System.Text.Json.Nodes;
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
            string formattedMessage = "";
            if (message.FormattedBody != null && message.FormattedBody.ContainsKey("html"))
            {
                formattedMessage = message.FormattedBody["html"];
            }
            var eventResponse = await sendTextMessageToRoomAsync(channelId, message.Body,
                message.ThreadingId, formattedMessage);
            return eventResponse.EventId;
        }

        public async Task<string> UpdateMessageAsync(string channelId, string messageId,
            ChatMessageModel message)
        {
            // TODO: Only support text messages for now
            var payload = new Dictionary<string, object>()
            {
                {
                    "m.new_content",
                    new Dictionary<string, object>()
                    {
                        { "msgtype", "m.text" },
                        { "body", message.Body },
                    }
                },
                {
                    "m.relates_to",
                    new Dictionary<string, object>()
                    {
                        { "rel_type", "m.replace" },
                        { "event_id", messageId },
                    }
                },
                { "msgtype", "m.text" },
                { "body", $"* {message.Body}" },
            };

            var payloadString = JsonSerializer.Serialize(payload);
            var content = new StringContent(payloadString, Encoding.UTF8, "application/json");
            var result = await _httpClient.PutAsync(
                $"/_matrix/client/r0/rooms/{HttpUtility.UrlEncode(channelId)}" +
                $"/send/m.room.message/{_nextTxnId++}", content);
            if (result.StatusCode != HttpStatusCode.OK)
            {
                var error = await result.Content.ReadFromJsonAsync<MatrixError>();
                throw new ApplicationException($"Received error sending room message " +
                    $"{error.ErrorCode}: {error.Error}");
            }
            var response = await result.Content.ReadFromJsonAsync<MatrixSendResponse>();
            return messageId;
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
            populateChannels(response);
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

        private void populateChannels(MatrixSyncResponse syncResponse)
        {
            if (syncResponse.Rooms?.JoinedRooms != null)
            {
                _channels.Clear();
                foreach (var room in syncResponse.Rooms.JoinedRooms)
                {
                    var roomId = room.Key;
                    var roomAlias = roomId;
                    var roomName = roomId;
                    var members = new List<UserModel>();
                    foreach (var roomEvent in room.Value.State.Events)
                    {
                        if (roomEvent is MatrixRoomCanonicalAliasEvent)
                        {
                            roomAlias = 
                                (roomEvent as MatrixRoomCanonicalAliasEvent).Content.RoomAlias;
                        }
                        if (roomEvent is MatrixRoomNameEvent)
                        {
                            roomName = (roomEvent as MatrixRoomNameEvent).Content.Name;
                        }
                        if (roomEvent is MatrixRoomMemberEvent)
                        {
                            var memberEvent = roomEvent as MatrixRoomMemberEvent;
                            if (!memberEvent.Content.Membership.Equals("join",
                                StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                            var member = new UserModel()
                            {
                                Id = memberEvent.Sender,
                                Provider = ChatServiceKind.Matrix,
                                Alias = memberEvent.Sender,
                                DisplayName = memberEvent.Content.DisplayName,
                                Presence = UserModel.PresenceKind.Online,
                                FromChatServiceConnection = this,
                            };
                            members.Add(member);
                        }
                    }
                    
                    _channels.Add(new ChannelModel()
                    {
                        Id = roomId,
                        Alias = roomAlias,
                        DisplayName = roomName,
                        Users = members,
                    });
                }
            }
            onChannelsUpdated();
        }

        private void processRoomMessage(string roomId, MatrixRoomMessageEvent message)
        {
            string body = "";
            List<AttachmentModel> attachments = new List<AttachmentModel>();
            if (message.Content is MatrixRoomMessageEventTextContent)
            {
                body = (message.Content as MatrixRoomMessageEventTextContent).Body;
            }
            else if (message.Content is MatrixRoomMessageEventImageContent)
            {
                var imageContent = message.Content as MatrixRoomMessageEventImageContent;

                // Reconstruct download URL from MXC URI as per
                // https://spec.matrix.org/v1.3/client-server-api/#get_matrixmediav3downloadservernamemediaidfilename
                var mxcUrl = new Uri(imageContent.Url);
                string attachmentUrl = $"{_serverBaseUrl}/_matrix/media/v3/download/" +
                    $"{mxcUrl.Host}{mxcUrl.LocalPath}/file";

                attachments.Add(new AttachmentModel()
                {
                    Kind = AttachmentKind.Image,
                    Name = imageContent.Body,
                    Uri = attachmentUrl,
                });
            }
            else
            {
                _logger.LogInformation($"Ignoring message type '{message.Content.MessageType}'");
                return;
            }

            string alias = message.Sender;
            string displayName = message.Sender;
            var channel = _channels.SingleOrDefault(c => (c.Id == roomId));
            if (channel != null)
            {
                var sender = channel.Users.SingleOrDefault(u => (u.Id == message.Sender));
                alias = sender.Alias;
                displayName = sender.DisplayName;
            }
            var messageModel = new ChatMessageModel()
            {
                Id = message.EventId,
                Provider = ChatServiceKind.Matrix,
                ProviderInstance = Name,
                AuthorId = message.Sender,
                AuthorAlias = alias,
                AuthorDisplayName = displayName,
                Body = body,
                ChannelId = roomId,
                TimeSent = DateTimeOffset.FromUnixTimeMilliseconds(
                    (long)message.OriginServerTimestamp),
                ThreadingId = "",
                Attachments = attachments,
                FromChatServiceConnection = this,
                IsFromTheorem = (message.Sender.Equals(_userId)),
                IsMentioningTheorem = false,
            };
            onNewMessage(messageModel);
        }

        private async Task<MatrixSendResponse> sendTextMessageToRoomAsync(string roomId,
            string body, string threadEventId = "", string formattedMessage = "")
        {
            // https://matrix.org/docs/spec/client_server/latest#id271
            var payload = new JsonObject
            {
                ["msgtype"] = "m.text",
                ["body"] = body,
            };
            if (threadEventId != "")
            {
                payload.Add("m.relates_to", new JsonObject
                {
                    ["rel_type"] = "m.thread",
                    ["event_id"] = threadEventId,
                });
            }
            if (formattedMessage != "")
            {
                payload["format"] = "org.matrix.custom.html";
                payload["formatted_body"] = formattedMessage;
            }
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