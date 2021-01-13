using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProtoBuf;
using Theorem.Models;
using Theorem.Models.Mumble;
using Theorem.Models.Mumble.MumbleProto;
using Version = Theorem.Models.Mumble.MumbleProto.Version;

namespace Theorem.ChatServices
{
    public class MumbleChatServiceConnection :
        IChatServiceConnection
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

        /// <summary>
        /// Collection of users present on this chat service connection
        /// </summary>
        public ObservableCollection<UserModel> Users { get; private set; }
            = new ObservableCollection<UserModel>();

        /// <summary>
        /// Collection of users currently online on this chat service connection
        /// </summary>
        public ObservableCollection<UserModel> OnlineUsers
        {
            get
            {
                return Users;
            }
        }

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

        public event EventHandler<EventArgs> Connected;
        public event EventHandler<ChatMessageModel> NewMessage;

        public async Task<string> GetChannelIdFromChannelNameAsync(string channelName)
        {
            var matchingChannel = 
                _mumbleChannels.Values.SingleOrDefault(c => c.Name == channelName);
            if (matchingChannel != null)
            {
                return matchingChannel.ChannelId.ToString();
            }
            else
            {
                return "";
            }
        }

        public async Task SendMessageToChannelIdAsync(string channelId, string body)
        {
            var textMessage = new TextMessage
            {
                ChannelIds = new uint[]{ UInt32.Parse(channelId) },
                Message = body
            };
            await sendAsync<TextMessage>(PacketType.TextMessage, textMessage);
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

        public async Task StartAsync()
        {
            // Clear out our members
            Users.Clear();
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
                (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors) => true,
                (object sender, string targetHost, X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers) => null);
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
            var pingTimer = new System.Threading.Timer(async (state) => await sendPingAsync(), null, 5000, 5000);

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
        }

        private void processUserState(UserState userState)
        {
            if (!(_mumbleUsers.ContainsKey(userState.Session)))
            {
                _mumbleUsers[userState.Session] = userState;
                _logger.LogInformation($"Received new user state for '{userState.Name}'.");
                this.Users.Add(new UserModel
                {
                    FromChatServiceConnection = this,
                    Id = userState.Session.ToString(),
                    DisplayName = userState.Name,
                    Name = userState.Name,
                    Provider = ChatServiceKind.Mumble,
                });
            }
            else
            {
                var cachedUserState = _mumbleUsers[userState.Session];
                var cachedUserModel = 
                    Users.SingleOrDefault(u => u.Id == userState.Session.ToString());
                if (userState.ShouldSerializeName())
                {
                    cachedUserState.Name = userState.Name;
                    cachedUserModel.Name = userState.Name;
                }
                if (userState.ShouldSerializeChannelId())
                {
                    cachedUserState.ChannelId = userState.ChannelId;
                }
                _logger.LogInformation($"Received updated user state for '{cachedUserState.Name}'.");
            }
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
            var cachedUserModel = Users.SingleOrDefault(u => u.Id == userRemove.Session.ToString());
            if (cachedUserModel != null)
            {
                Users.Remove(cachedUserModel);
            }
        }

        private void processTextMessage(TextMessage textMessage)
        {
            if (textMessage.ChannelIds == null)
            {
                var message = new ChatMessageModel
                {
                    FromChatServiceConnection = this,
                    Id = "",
                    AuthorId = textMessage.Actor.ToString(),
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
                Serializer.SerializeWithLengthPrefix<T>(_sslStream, packet, PrefixStyle.Fixed32BigEndian);
                await _sslStream.FlushAsync();
            }
            finally
            {
                _streamWriteSemaphor.Release();
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
            var eventHandler = NewMessage;
            if (eventHandler != null)
            {
                eventHandler(this, message);
            }
        }

        public async Task SetChannelTopicAsync(string channelId, string topic)
        {
            // TODO
        }

        public async Task<int> GetMemberCountFromChannelIdAsync(string channelId)
        {
            // TODO
            return 0;
        }
    }
}