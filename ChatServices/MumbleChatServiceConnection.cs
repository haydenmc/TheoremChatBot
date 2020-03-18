using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MumbleProto;
using MumbleSharp;
using MumbleSharp.Audio;
using MumbleSharp.Audio.Codecs;
using MumbleSharp.Model;
using Theorem.Models;

namespace Theorem.ChatServices
{
    public class TheoremMumbleProtocol :
        IMumbleProtocol
    {
        public MumbleConnection Connection { get; private set; }

        public User LocalUser { get; private set; }

        public Channel RootChannel { get; private set; }

        public IEnumerable<Channel> Channels 
        {
            get
            {
                return _channelDictionary.Values;
            }
        }

        private readonly ConcurrentDictionary<UInt32, Channel> _channelDictionary = 
            new ConcurrentDictionary<UInt32, Channel>();

        public IEnumerable<User> Users {
            get
            {
                return _userDictionary.Values;
            }
        }
        private readonly ConcurrentDictionary<UInt32, User> _userDictionary = 
            new ConcurrentDictionary<UInt32, User>();

        public bool ReceivedServerSync { get; private set; }

        public SpeechCodecs TransmissionCodec
        {
            get
            {
                return SpeechCodecs.Opus;
            }
        }

        public TheoremMumbleProtocol()
        {

        }

        public void Initialise(MumbleConnection connection)
        {
            Connection = connection;
        }

        public void Acl(Acl acl)
        { }

        public void BanList(BanList banList)
        { }

        public void ChannelRemove(ChannelRemove channelRemove)
        { }

        public void ChannelState(ChannelState channelState)
        { }

        public void CodecVersion(CodecVersion codecVersion)
        { }

        public void ContextAction(ContextAction contextAction)
        { }

        public void EncodedVoice(byte[] packet, uint userSession, long sequence, IVoiceCodec codec, SpeechTarget target)
        { }

        public IVoiceCodec GetCodec(uint user, SpeechCodecs codec)
        {
            return null;
        }

        public void PermissionDenied(PermissionDenied permissionDenied)
        { }

        public void PermissionQuery(PermissionQuery permissionQuery)
        { }

        public void Ping(Ping ping)
        { }

        public void QueryUsers(QueryUsers queryUsers)
        { }

        public void Reject(Reject reject)
        { }

        public X509Certificate SelectCertificate(object sender, string targetHost, X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers)
        {
            return null; // Something something security..?
                         // Not my fault, original implementation does this ;)
        }

        public void SendVoice(ArraySegment<byte> pcm, SpeechTarget target, uint targetId)
        {
            throw new NotImplementedException();
        }

        public void SendVoiceStop()
        {
            throw new NotImplementedException();
        }

        public void ServerConfig(ServerConfig serverConfig)
        { }

        public void ServerSync(ServerSync serverSync)
        {
            if (LocalUser != null)
            {
                throw new InvalidOperationException("Second ServerSync Received");
            }

            if (!serverSync.ShouldSerializeSession())
            {
                throw new InvalidOperationException(
                    $"{nameof(ServerSync)} must provide a {nameof(serverSync.Session)}.");
            }

            //Get the local user
            LocalUser = _userDictionary[serverSync.Session];

            ReceivedServerSync = true;
        }

        public void SuggestConfig(SuggestConfig suggestedConfiguration)
        { }

        public void TextMessage(TextMessage textMessage)
        { }

        public void UdpPing(byte[] packet)
        { }

        public void UserList(UserList userList)
        { }

        /// <summary>
        /// Used to communicate user leaving or being kicked.
        /// Sent by the server when it informs the clients that a user is not present anymore.
        /// </summary>
        public void UserRemove(UserRemove userRemove)
        {
            User user;
            if (_userDictionary.TryRemove(userRemove.Session, out user))
            {
                user.Channel = null;
                //UserLeft(user);
            }

            if (user != null && user.Equals(LocalUser))
            {
                Connection.Close();
            }
        }

        /// <summary>
        /// Sent by the server when it communicates new and changed users to client.
        /// First seen during login procedure.
        /// </summary>
        public void UserState(UserState userState)
        {
            if (userState.ShouldSerializeSession())
            {
                User user;
                if (_userDictionary.ContainsKey(userState.Session))
                {
                    user = _userDictionary[userState.Session];
                }
                else
                {
                    user = new User(this, userState.Session);
                    _userDictionary[userState.Session] = user;
                }

                // Update user in the dictionary
                if (userState.ShouldSerializeSelfDeaf())
                {
                    user.SelfDeaf = userState.SelfDeaf;
                }
                if (userState.ShouldSerializeSelfMute())
                {
                    user.SelfMuted = userState.SelfMute;
                }
                if (userState.ShouldSerializeMute())
                {
                    user.Muted = userState.Mute;
                }
                if (userState.ShouldSerializeDeaf())
                {
                    user.Deaf = userState.Deaf;
                }
                if (userState.ShouldSerializeSuppress())
                {
                    user.Suppress = userState.Suppress;
                }
                if (userState.ShouldSerializeName())
                {
                    user.Name = userState.Name;
                }
                if (userState.ShouldSerializeComment())
                {
                    user.Comment = userState.Comment;
                }

                if (userState.ShouldSerializeChannelId())
                {
                    user.Channel = _channelDictionary[userState.ChannelId];
                }
                else if (user.Channel == null)
                {
                    user.Channel = RootChannel;
                }

                //if (added)
                // UserJoined(user);
            }
        }

        public void UserStats(UserStats userStats)
        { }

        public bool ValidateCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
        {
            return true; // Something something security..?
                         // Not my fault, original implementation does this ;)
        }

        public void Version(MumbleProto.Version version)
        { }
    }

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

        public string UserId => throw new NotImplementedException();

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

        public event EventHandler<EventArgs> Connected;
        public event EventHandler<ChatMessageModel> NewMessage;

        public async Task<string> GetChannelIdFromChannelNameAsync(string channelName)
        {
            return "";
        }

        public async Task SendMessageToChannelIdAsync(string channelId, string body)
        {
            
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
            _logger.LogInformation("Connecting to Mumble server {server}...",
                _serverHostname);

            TheoremMumbleProtocol protocol = new TheoremMumbleProtocol();
            IPAddress ipAddress = Dns
                .GetHostAddresses(_serverHostname)
                .First(a => a.AddressFamily == AddressFamily.InterNetwork);
            MumbleConnection connection = 
                new MumbleConnection(new IPEndPoint(ipAddress, (int)_serverPort), protocol);
            connection.Connect(_username, _serverPassword, new string[0], _serverHostname);

            await Task.WhenAll(new Task[]
            {
                mumbleUpdateLoop(connection),
                waitForConnection(protocol)
            });
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

        private async Task waitForConnection(IMumbleProtocol protocol)
        {
            await Task.Run(() => {
                while (!protocol.ReceivedServerSync)
                { }
                _logger.LogInformation("{name} Mumble connection established", Name);
                onConnected();
            });
        }

        private async Task mumbleUpdateLoop(MumbleConnection connection)
        {
            await Task.Run(() => {
                while (connection.State != ConnectionStates.Disconnected)
                {
                    if (connection.Process())
                    {
                        Thread.Yield();
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
            });
        }
    }
}