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
                return this._username;
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

        private SslStream _sslStream;

        private SemaphoreSlim _streamWriteSemaphor;

        public event EventHandler<EventArgs> Connected;
        public event EventHandler<ChatMessageModel> NewMessage;

        public async Task<string> GetChannelIdFromChannelNameAsync(string channelName)
        {
            // TODO
            return "";
        }

        public async Task SendMessageToChannelIdAsync(string channelId, string body)
        {
            // TODO
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

            // Send handshake
            await send<Version>(
                _sslStream,
                PacketType.Version,
                new Version
                {
                    Release = "Theorem",
                    version = (1 << 16) | (2 << 8) | (0 & 0xFF),
                    Os = Environment.OSVersion.ToString(),
                    OsVersion = Environment.OSVersion.VersionString
                });

            // Send auth
            await send<Authenticate>(
                _sslStream,
                PacketType.Authenticate,
                new Authenticate
                {
                    Username = _username,
                    Password = _serverPassword,
                    Opus = false
                });

            // Now just read 4eva
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
                    case PacketType.Version:
                        Serializer.DeserializeWithLengthPrefix<Version>(_sslStream, PrefixStyle.Fixed32BigEndian);
                        break;
                    case PacketType.CryptSetup:
                        {
                            var cryptSetup = Serializer.DeserializeWithLengthPrefix<CryptSetup>(
                                _sslStream, PrefixStyle.Fixed32BigEndian);
                            await send<Ping>(_sslStream, PacketType.Ping, new Ping());
                        }
                        break;
                    default:
                        int payloadLength;
                        if (Serializer.TryReadLengthPrefix(
                            _sslStream,
                            PrefixStyle.Fixed32BigEndian,
                            out payloadLength))
                        {
                            _logger.LogDebug($"Packet type {type.ToString()} received with " + 
                                $"{payloadLength} byte payload.");
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
                                _logger.LogDebug($"Read {bytesRead} bytes.");
                            }
                            else
                            {
                                _logger.LogWarning($"Invalid payload length.");
                            }
                        }
                        else
                        {
                            _logger.LogDebug($"Packet type {type.ToString()} received, " + 
                                $"could not read payload.");
                        }
                        break;
                }
            }
        }

        private async Task send<T>(PacketType type, T packet)
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