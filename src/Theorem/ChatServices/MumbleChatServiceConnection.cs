using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProtoBuf;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Theorem.Models;
using Theorem.Models.Mumble;
using Theorem.Models.Mumble.MumbleProto;
using Version = Theorem.Models.Mumble.MumbleProto.Version;

namespace Theorem.ChatServices
{
    public class MumbleChatServiceConnection : IChatServiceConnection
    {
        /// <summary>
        /// Configuration object for retrieving configuration values
        /// </summary>
        private ConfigurationSection _configuration { get; set; }

        /// <summary>
        /// Logger instance for logging events
        /// </summary>
        private ILogger<MumbleChatServiceConnection> _logger { get; set; }
        
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

        /// <summary>
        /// User ID assigned to us by Mumble
        /// </summary>
        public string UserId
        {
            get
            {
                return "";
            }
        }

        /// <summary>
        /// User name of the bot in this service
        /// </summary>
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

        public event EventHandler<EventArgs> Connected;

        public event EventHandler<ChatMessageModel> MessageReceived;

        public event EventHandler<ICollection<ChannelModel>> ChannelsUpdated;

        /// <summary>
        /// Whether or not we are currently connected to the chat service
        /// </summary>
        public bool IsConnected { get; private set; } = false;

        /// <summary>
        /// Hostname of the server to connect to defined by configuration values
        /// </summary>
        private string _serverHostname
        {
            get
            {
                return _configuration["ServerHostname"];
            }
        }

        /// <summary>
        /// Port of the server to connect to defined by configuration values
        /// </summary>
        private uint _serverPort
        {
            get
            {
                return uint.Parse(_configuration["ServerPort"]);
            }
        }

        /// <summary>
        /// Username to use when connecting as defined by configuration values
        /// </summary>
        private string _username
        {
            get
            {
                if (_configuration["Username"].Length > 0)
                {
                    return _configuration["Username"];
                }
                else
                {
                    return "Theorem";
                }
            }
        }

        /// <summary>
        /// Password of the server to connect to defined by configuration values
        /// </summary>
        private string _serverPassword
        {
            get
            {
                return _configuration["ServerPassword"];
            }
        }

        /// <summary>
        /// Channel for Theorem to join
        /// </summary>
        private string _channelNameToJoin
        {
            get
            {
                return _configuration["Channel"];
            }
        }

        private SslStream _sslStream;

        private SemaphoreSlim _streamWriteSemaphor;

        private Dictionary<uint, ChannelState> _mumbleChannels = new Dictionary<uint, ChannelState>();

        private Dictionary<uint, UserState> _mumbleUsers = new Dictionary<uint, UserState>();

        private uint _mumbleSessionId;

        private List<ChannelModel> _channels = new List<ChannelModel>();

        public Task<string> GetChannelIdFromChannelNameAsync(string channelName)
        {
            var matchingChannel = 
                _mumbleChannels.Values.SingleOrDefault(c => c.Name == channelName);
            if (matchingChannel != null)
            {
                return Task.FromResult(matchingChannel.ChannelId.ToString());
            }
            else
            {
                return Task.FromResult("");
            }
        }

        public async Task<string> SendMessageToChannelIdAsync(string channelId, ChatMessageModel message)
        {
            string messageText = message.Body;
            
            // Make URLs clickable
            var urlMatches = Regex.Matches(messageText,
                @"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\." + 
                @"[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)");
            
            // Process matches from the back of the string forward, to avoid offsetting our indexes
            foreach (Match match in urlMatches.OrderByDescending(u => u.Index))
            {
                var url = HttpUtility.HtmlAttributeEncode(match.Value);
                messageText = messageText.Substring(0, match.Index) +
                    $"<a href=\"{url}\">{match.Value}</a>" +
                    messageText.Substring(match.Index + match.Length);
            }

            // Insert any image attachments inline
            if (message.Attachments?.Count() > 0)
            {
                foreach (var attachment in message.Attachments)
                {
                    // Try to get a cute little thumbnail
                    var imageThumbnailBase64 = await getBase64ImagePreviewAsync(
                        new Uri(attachment.Uri));
                    var encodedBase64Uri = "data:image/jpeg;base64," + 
                        $"{HttpUtility.HtmlAttributeEncode(imageThumbnailBase64)}";
                    var encodedName = HttpUtility.HtmlEncode(attachment.Name);
                    var encodedUri = HttpUtility.HtmlAttributeEncode(attachment.Uri);
                    messageText += $"<br /><img src=\"{encodedBase64Uri}\" /><br />" +
                        $"<a href=\"{encodedUri}\">{encodedName}</a>";
                }
            }

            var textMessage = new TextMessage
            {
                ChannelIds = new uint[]{ UInt32.Parse(channelId) },
                Message = messageText
            };
            await sendAsync<TextMessage>(PacketType.TextMessage, textMessage);
            return ""; // Mumble does not assign IDs to messages
        }

        public async Task<string> UpdateMessageAsync(string channelId, string messageId,
            ChatMessageModel message)
        {
            _logger.LogWarning("Mumble does not support editing messages - " + 
                "sending a new message instead.");
            return await SendMessageToChannelIdAsync(channelId, message);
        }

        /// <summary>
        /// Constructs a new instance of MumbleChatServiceConnection.
        /// </summary>
        public MumbleChatServiceConnection(ConfigurationSection configuration,
            ILogger<MumbleChatServiceConnection> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task RunAsync()
        {
            // Clear out our members
            _channels.Clear();
            _mumbleUsers.Clear();
            _mumbleChannels.Clear();

            // Connect
            _logger.LogInformation("Connecting to Mumble server {server}...",
                _serverHostname);

            TcpClient tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(_serverHostname, (int)_serverPort);
            NetworkStream networkStream = tcpClient.GetStream();
            _sslStream = new SslStream(
                networkStream,
                false,
                (object sender, X509Certificate certificate, X509Chain chain,
                    SslPolicyErrors errors) => true,
                (object sender, string targetHost, X509CertificateCollection localCertificates,
                    X509Certificate remoteCertificate, string[] acceptableIssuers) => null);
            await _sslStream.AuthenticateAsClientAsync(_serverHostname);
            _streamWriteSemaphor = new SemaphoreSlim(1, 1);

            // Send version
            await sendAsync<Version>(
                PacketType.Version,
                new Version
                {
                    Release = "Theorem",
                    version = (1 << 16) | (2 << 8) | (0 & 0xFF),
                    Os = Environment.OSVersion.ToString(),
                    OsVersion = Environment.OSVersion.VersionString
                });

            // Send auth
            await sendAsync<Authenticate>(
                PacketType.Authenticate,
                new Authenticate
                {
                    Username = _username,
                    Password = _serverPassword,
                    Opus = false
                });

            // We need to send pings every so often, otherwise the server disconnects us
            var pingTimer = new System.Threading.Timer(async (state) => await sendPingAsync(),
                null, 5000, 5000);

            // Now just read incoming data forever
            try
            {
                await listenAsync();
            }
            finally
            {
                await pingTimer.DisposeAsync();
            }
        }

        private async Task listenAsync()
        {
            byte[] packetTypeBuffer = new byte[2];
            while (true)
            {
                int typeBytesRead = await _sslStream.ReadAsync(packetTypeBuffer, 0, 2);
                if (typeBytesRead == 0)
                {
                    _logger.LogWarning("Connection closed by server.");
                    break;
                }
                else if (typeBytesRead < 2)
                {
                    _logger.LogWarning("Couldn't read packet type.");
                    continue;
                }
                PacketType type = (PacketType)IPAddress.NetworkToHostOrder(
                    BitConverter.ToInt16(packetTypeBuffer));
                switch (type)
                {
                    case PacketType.ChannelState:
                        var channelState = 
                            Serializer.DeserializeWithLengthPrefix<ChannelState>(
                                _sslStream, PrefixStyle.Fixed32BigEndian);
                        processChannelState(channelState);
                        break;
                    case PacketType.ChannelRemove:
                        var channelRemove = 
                            Serializer.DeserializeWithLengthPrefix<ChannelRemove>(
                                _sslStream, PrefixStyle.Fixed32BigEndian);
                        processChannelRemove(channelRemove);
                        break;
                    case PacketType.UserState:
                        var userState = 
                            Serializer.DeserializeWithLengthPrefix<UserState>(
                                _sslStream, PrefixStyle.Fixed32BigEndian);
                        processUserState(userState);
                        break;
                    case PacketType.UserRemove:
                        var userRemove = 
                            Serializer.DeserializeWithLengthPrefix<UserRemove>(
                                _sslStream, PrefixStyle.Fixed32BigEndian);
                        processUserRemove(userRemove);
                        break;
                    case PacketType.TextMessage:
                        var textMessage = 
                            Serializer.DeserializeWithLengthPrefix<TextMessage>(
                                _sslStream, PrefixStyle.Fixed32BigEndian);
                        processTextMessage(textMessage);
                        break;
                    case PacketType.ServerSync:
                        // This indicates we are finally connected
                        var serverSync =
                            Serializer.DeserializeWithLengthPrefix<ServerSync>(
                                _sslStream, PrefixStyle.Fixed32BigEndian);
                        _mumbleSessionId = serverSync.Session;
                        // Join the channel we want to be in
                        await joinChannelAsync();
                        IsConnected = true;
                        updateChannelStore();
                        onConnected();
                        break;
                    default:
                        //_logger.LogDebug($"Packet type {type.ToString()} received");
                        await processUnknownPayloadAsync();
                        break;
                }
            }
            IsConnected = false;
        }

        private void processChannelState(ChannelState channelState)
        {
            if (!(_mumbleChannels.ContainsKey(channelState.ChannelId)))
            {
                _mumbleChannels[channelState.ChannelId] = channelState;
                _logger.LogInformation($"Received new channel state for '{channelState.Name}'.");
            }
            else
            {
                var cachedChannelState = _mumbleChannels[channelState.ChannelId];
                if (channelState.ShouldSerializeName())
                {
                    cachedChannelState.Name = channelState.Name;
                }
                _logger.LogInformation($"Received updated channel state for " + 
                    $"'{cachedChannelState.Name}'.");
            }
            updateChannelStore();
        }

        private void processChannelRemove(ChannelRemove channelRemove)
        {
            if (!(_mumbleChannels.Remove(channelRemove.ChannelId)))
            {
                _logger.LogError($"Server requested removal of channel ID " + 
                    $"'{channelRemove.ChannelId}' that does not exist in cache.");
            }
            else
            {
                _logger.LogInformation($"Received channel removal for ID " + 
                    $"'{channelRemove.ChannelId}'.");
            }
            updateChannelStore();
        }

        private void processUserState(UserState userState)
        {
            if (!(_mumbleUsers.ContainsKey(userState.Session)))
            {
                _mumbleUsers[userState.Session] = userState;
                _logger.LogInformation($"Received new user state for '{userState.Name}'.");
            }
            else
            {
                var cachedUserState = _mumbleUsers[userState.Session];
                if (userState.ShouldSerializeName())
                {
                    cachedUserState.Name = userState.Name;
                }
                if (userState.ShouldSerializeChannelId())
                {
                    cachedUserState.ChannelId = userState.ChannelId;
                }
                _logger.LogInformation("Received updated user state for " +
                    $"'{cachedUserState.Name}'.");
            }
            updateChannelStore();
        }

        private void processUserRemove(UserRemove userRemove)
        {
            if (!(_mumbleUsers.Remove(userRemove.Session)))
            {
                _logger.LogError($"Server requested removal of user session ID " + 
                    $"'{userRemove.Session}' that does not exist in cache.");
            }
            else
            {
                _logger.LogInformation($"Received user removal for session ID " + 
                    $"'{userRemove.Session}'.");
            }
            updateChannelStore();
        }

        private void processTextMessage(TextMessage textMessage)
        {
            // Hackily strip any yucky HTML that mumble inserts...
            textMessage.Message = Regex.Replace(textMessage.Message, "<[^>]*(>|$)", string.Empty);
            var alias = textMessage.Actor.ToString();
            var displayName = textMessage.Actor.ToString();
            if (_mumbleUsers.ContainsKey(textMessage.Actor))
            {
                var user = _mumbleUsers[textMessage.Actor];
                alias = user.Name;
                displayName = user.Name;
            }
            if (textMessage.ChannelIds == null)
            {
                var message = new ChatMessageModel
                {
                    FromChatServiceConnection = this,
                    Id = "",
                    AuthorId = textMessage.Actor.ToString(),
                    AuthorAlias = alias,
                    AuthorDisplayName = displayName,
                    Body = textMessage.Message,
                    ChannelId = "",
                    Provider = ChatServiceKind.Mumble,
                    ProviderInstance = Name,
                    TimeSent = DateTimeOffset.Now,
                    IsFromTheorem = (textMessage.Actor == _mumbleSessionId)
                };
                onNewMessage(message);
            }
            else
            {
                foreach (var channel in textMessage.ChannelIds)
                {
                    var message = new ChatMessageModel
                    {
                        FromChatServiceConnection = this,
                        Id = "",
                        AuthorId = textMessage.Actor.ToString(),
                        AuthorAlias = alias,
                        AuthorDisplayName = displayName,
                        Body = textMessage.Message,
                        ChannelId = channel.ToString(),
                        Provider = ChatServiceKind.Mumble,
                        ProviderInstance = Name,
                        TimeSent = DateTimeOffset.Now,
                        IsFromTheorem = (textMessage.Actor == _mumbleSessionId)
                    };
                    onNewMessage(message);
                }
            }
        }

        private async Task joinChannelAsync()
        {
            if (_channelNameToJoin.Length <= 0)
            {
                _logger.LogInformation("No starting channel was specified to join.");
                return;
            }
            var targetChannel = _mumbleChannels.Values.Single(c => c.Name == _channelNameToJoin);
            if (targetChannel != null)
            {
                var userState = new UserState()
                {
                    ChannelId = targetChannel.ChannelId
                };
                await sendAsync<UserState>(PacketType.UserState, userState);
            }
            else
            {
                _logger.LogError($"Could not find channel '{_channelNameToJoin}' " + 
                    $"when attempting to join.");
            }
        }

        private async Task processUnknownPayloadAsync()
        {
            int payloadLength;
            if (Serializer.TryReadLengthPrefix(
                _sslStream,
                PrefixStyle.Fixed32BigEndian,
                out payloadLength))
            {
                if (payloadLength > 0)
                {
                    // read out those bytes
                    byte[] readBuf = new byte[8];
                    int bytesRead = 0;
                    while (bytesRead < payloadLength)
                    {
                        bytesRead += await _sslStream.ReadAsync(
                            readBuf,
                            0,
                            Math.Min(readBuf.Length, (payloadLength - bytesRead)));
                    }
                    //_logger.LogDebug($"Read {bytesRead} bytes.");
                }
                else
                {
                    _logger.LogWarning($"Invalid payload length.");
                }
            }
            else
            {
                _logger.LogError($"Could not read payload.");
            }
        }

        private async Task sendPingAsync()
        {
            await sendAsync<Ping>(PacketType.Ping, new Ping());
        }

        private async Task sendAsync<T>(PacketType type, T packet)
        {
            await _streamWriteSemaphor.WaitAsync();
            try
            {
                byte[] packetTypeBytes = 
                    BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)type));
                await _sslStream.WriteAsync(packetTypeBytes, 0, packetTypeBytes.Length);
                Serializer.SerializeWithLengthPrefix<T>(_sslStream, packet,
                    PrefixStyle.Fixed32BigEndian);
                await _sslStream.FlushAsync();
            }
            finally
            {
                _streamWriteSemaphor.Release();
            }
        }

        private void updateChannelStore()
        {
            if (!IsConnected)
            {
                return;
            }
            var channelModelsById = new Dictionary<uint, ChannelModel>();
            foreach (var channel in _mumbleChannels.Values)
            {
                var channelModel = new ChannelModel()
                {
                    Id = channel.ChannelId.ToString(),
                    Alias = channel.Name,
                    DisplayName = channel.Name,
                    Users = new List<UserModel>(),
                };
                channelModelsById[channel.ChannelId] = channelModel;
            }

            foreach (var user in _mumbleUsers.Values)
            {
                var userModel = new UserModel()
                {
                    Id = user.Name,
                    IsTheorem = (user.Session == _mumbleSessionId),
                    Provider = ChatServiceKind.Mumble,
                    Alias = user.Name,
                    DisplayName = user.Name,
                    Presence = UserModel.PresenceKind.Online,
                    FromChatServiceConnection = this,
                };
                if (channelModelsById.ContainsKey(user.ChannelId))
                {
                    channelModelsById[user.ChannelId].Users.Add(userModel);
                }
                else
                {
                    _logger.LogWarning($"Couldn't find channel {user.ChannelId} for Mumble user " +
                        $"{user.UserId} / {user.Name}.");
                }
            }
            _channels = channelModelsById.Values.ToList();
            onChannelsUpdated();
        }

        private static async Task<string> getBase64ImagePreviewAsync(Uri imageUri)
        {
            using (var httpClient = new HttpClient())
            using (var imageStream = await httpClient.GetStreamAsync(imageUri))
            using (var image = await Image.LoadAsync(imageStream))
            {
                var targetWidth = 128;
                var targetHeight = image.Height * (targetWidth / image.Width);
                image.Mutate(i => i.Resize(targetWidth, targetHeight));

                var thumbnailStream = new MemoryStream();
                await image.SaveAsJpegAsync(thumbnailStream);
                return Convert.ToBase64String(thumbnailStream.ToArray());
            }
        }

        /// <summary>
        /// Used to raise the Connected event.
        /// </summary>
        protected virtual void onConnected()
        {
            var eventHandler = Connected;
            if (eventHandler != null)
            {
                eventHandler(this, EventArgs.Empty);
            }
        }
        
        /// <summary>
        /// Used to raise the NewMessage event.
        /// </summary>
        /// <param name="message">Event arguments; the message that was received</param>
        protected virtual void onNewMessage(ChatMessageModel message)
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

        public Task SetChannelTopicAsync(string channelId, string topic)
        {
            _logger.LogWarning("Attempted to set Mumble channel topic, but Mumble does not " + 
                "support channel topics.");
            return Task.CompletedTask;
        }
    }
}