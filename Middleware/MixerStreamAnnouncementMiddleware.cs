using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Theorem.ChatServices;
using Theorem.Models;

namespace Theorem.Middleware
{
    // Mixer dev lab, since this is surprisingly hard to find:
    // https://mixer.com/lab/oauth
    public class MixerStreamAnnouncementMiddleware :
        IMiddleware
    {
        /// <summary>
        /// Logging instance
        /// </summary>
        private ILogger<MixerStreamAnnouncementMiddleware> _logger;

        /// <summary>
        /// Configuration object for retrieving configuration values
        /// </summary>
        private ConfigurationSection _configuration;

        /// <summary>
        /// List of chat service connections that we can use to announce streams.
        /// </summary>
        private IEnumerable<IChatServiceConnection> _chatServiceConnections;

        /// <summary>
        /// Hostname of the Mixer REST API
        /// </summary>
        private const string _restHostname = "mixer.com";

        /// <summary>
        /// Base path on the Mixer REST API service.
        /// </summary>
        private const string _restBasePath = "api/v1";

        /// <summary>
        /// Hostname of the Mixer websocket service
        /// </summary>
        private const string _webSocketHostname = "constellation.mixer.com";

        /// <summary>
        /// Client ID used to authenticate to Mixer.
        /// </summary>
        private string _clientId
        {
            get
            {
                return _configuration["ClientId"];
            }
        }

        /// <summary>
        /// The list of user names from configuration to subscribe to
        /// </summary>
        private IEnumerable<string> _usernames
        {
            get
            {
                return _configuration
                    .GetSection("MixerChannels")
                    .GetChildren()
                    .Select(c => c.Value);
            }
        }

        /// <summary>
        /// Mapping of Mixer usernames to respective channel IDs
        /// </summary>
        private Dictionary<string, UInt64> _mixerUsernameChannelIdMap
            = new Dictionary<string, UInt64>();

        /// <summary>
        /// Tracks the current request index used to correlate responses to Mixer websocket methods.
        /// </summary>
        private UInt64 _currentMixerRequestIndex = 0;

        /// <summary>
        /// Tracks channels for each chat service instance where we should announce streams.
        /// </summary>
        private Dictionary<IChatServiceConnection, string> _chatServiceAnnounceChannelId
            = new Dictionary<IChatServiceConnection, string>();


        /// <summary>
        /// Contains a set of channel IDs that are already marked as streaming
        /// </summary>
        private HashSet<UInt64> _currentlyStreamingChannelIds =
            new HashSet<UInt64>();

        public MixerStreamAnnouncementMiddleware(
            ILogger<MixerStreamAnnouncementMiddleware> logger,
            ConfigurationSection configuration,
            IEnumerable<IChatServiceConnection> chatServiceConnections)
        {
            _logger = logger;
            _configuration = configuration;
            _chatServiceConnections = chatServiceConnections;

            _logger.LogInformation("Starting MixerStreamAnnouncement Middleware...");
            subscribeToChatServiceConnectedEvents();
            startMixerConnection();
        }

        public MiddlewareResult ProcessMessage(ChatMessageModel message)
        {
            return MiddlewareResult.Continue;
        }

        private void subscribeToChatServiceConnectedEvents()
        {
            foreach (var chatService in _chatServiceConnections)
            {
                chatService.Connected += onChatServiceConnected;
            }
        }

        private async void onChatServiceConnected(object sender, EventArgs e)
        {
            var connection = sender as IChatServiceConnection;
            var matchingService = _configuration
                .GetSection("AnnounceChannels")
                .GetChildren()
                .SingleOrDefault(s => s.GetValue<string>("ChatServiceName") == connection.Name);
            if (matchingService != null)
            {
                var channelName = matchingService.GetValue<string>("ChannelName");
                var channelId = await connection.GetChannelIdFromChannelNameAsync(channelName);
                _chatServiceAnnounceChannelId[connection] = channelId;
                _logger.LogInformation(
                    "Chat service connection {name} connected, using channel {channel}:{id}.",
                    connection.Name,
                    channelName,
                    channelId);
            }
        }

        /// <summary>
        /// Returns an http client prepared with the correct base URL and authorization headers.
        /// </summary>
        private HttpClient getHttpClient()
        {
            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri($"https://{_restHostname}");
            bool headerSuccess = true;
            headerSuccess &= httpClient
                .DefaultRequestHeaders
                .TryAddWithoutValidation("Client-ID", $"{_clientId}");
            headerSuccess &= httpClient
                .DefaultRequestHeaders
                .TryAddWithoutValidation("x-is-bot", "true");
            if (!headerSuccess)
            {
                _logger.LogError("Couldn't set headers on Mixer REST HttpClient.");
            }
            return httpClient;
        }

        private async void startMixerConnection()
        {
            _logger.LogInformation("Looking up Channel IDs to subscribe to...");
            foreach (var username in _usernames)
            {
                UInt64 channelId = await getChannelIdFromUsername(username);
                if (channelId > 0)
                {
                    _logger.LogInformation("Found Mixer channel ID {id} for user {user}",
                        channelId,
                        username);
                    _mixerUsernameChannelIdMap[username] = channelId;
                }
            }

            _logger.LogInformation("Connecting to Mixer service...");
            var webSocketClient = new ClientWebSocket();
            webSocketClient.Options.SetRequestHeader("Client-ID", $"{_clientId}");
            webSocketClient.Options.SetRequestHeader("x-is-bot", "true");
            await webSocketClient.ConnectAsync(
                new Uri($"wss://{_webSocketHostname}"),
                CancellationToken.None);
            _logger.LogInformation("Connected to Mixer!");
            await Task.WhenAll(new Task[]
                {
                    receive(webSocketClient),
                    subscribeToChannels(webSocketClient)
                });
        }

        private async Task<UInt64> getChannelIdFromUsername(string username)
        {
            using (var httpClient = getHttpClient())
            {
                var result = await httpClient.GetAsync($"{_restBasePath}/channels/{username}?fields=id");
                if (!result.IsSuccessStatusCode)
                {
                    _logger.LogError("Encountered error code {code} calling {uri}: {msg}",
                        result.StatusCode,
                        result.Headers.Location,
                        result.ReasonPhrase);
                    return 0;
                }
                else
                {
                    try
                    {
                        var content = await result.Content.ReadAsStringAsync();
                        var parsedContent = JObject.Parse(content);
                        if (parsedContent.ContainsKey("id"))
                        {
                            return parsedContent.GetValue("id").ToObject<UInt64>();
                        }
                        else
                        {
                            _logger.LogError(
                                "Couldn't find 'id' value in channel ID response: {response}",
                                content);
                            return 0;
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError("Exception while querying for Channel ID: {msg}",
                            e.Message);
                        return 0;
                    }
                }
            }
        }

        private async Task subscribeToChannels(ClientWebSocket webSocketClient)
        {
            string[] eventList = _mixerUsernameChannelIdMap
                .Select(kv => $"channel:{kv.Value}:update")
                .ToArray();
            dynamic methodPayload = new
            {
                type = "method",
                method = "livesubscribe",
                @params = new
                {
                    events = eventList
                },
                id = _currentMixerRequestIndex
            };
            string methodPayloadString = JsonConvert.SerializeObject(methodPayload);
            _logger.LogDebug("Sending method to subscribe to channel updates: {payload}",
                methodPayloadString);
            await webSocketClient.SendAsync(
                Encoding.UTF8.GetBytes(methodPayloadString),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }

        /// <summary>
        /// Processes raw incoming websocket data from Mixer
        /// </summary>
        /// <param name="webSocketClient">
        /// The web socket client object receiving from
        /// </param>
        private async Task receive(ClientWebSocket webSocketClient)
        {
            while (webSocketClient.State == WebSocketState.Open)
            {
                StringBuilder messageBuilder = new StringBuilder();
                byte[] buffer = new byte[1024];
                WebSocketReceiveResult result;
                do
                {
                    result = await webSocketClient.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocketClient.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            string.Empty,
                            CancellationToken.None);
                    }
                    messageBuilder.Append(
                        Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!result.EndOfMessage);
                
                string messageString = messageBuilder.ToString();
                _logger.LogDebug("Received message: {message}", messageString);
                try
                {
                    var parsedMessage = JObject.Parse(messageString);
                    if (parsedMessage.SelectToken("data.payload.online") != null)
                    {
                        var channelBlob = 
                            parsedMessage.SelectToken("data.channel").ToObject<string>();
                        var isOnline = 
                            parsedMessage.SelectToken("data.payload.online").ToObject<bool>();
                        var channelIdStr = channelBlob.Substring(
                            channelBlob.IndexOf(':') + 1,
                            channelBlob.LastIndexOf(':') - (channelBlob.IndexOf(':') + 1));
                        var channelId = UInt64.Parse(channelIdStr);
                        if (isOnline)
                        {
                            if (!_currentlyStreamingChannelIds.Contains(channelId))
                            {
                                _currentlyStreamingChannelIds.Add(channelId);
                                await sendStreamingAnnouncement(channelId);
                            }
                        }
                        else
                        {
                            if (_currentlyStreamingChannelIds.Contains(channelId))
                            {
                                _currentlyStreamingChannelIds.Remove(channelId);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError("Could not process message from Mixer: {message}\n{e}",
                        messageString,
                        e.Message);
                }
            }
        }

        private async Task sendStreamingAnnouncement(UInt64 mixerChannelId)
        {
            var username = _mixerUsernameChannelIdMap
                .SingleOrDefault(kv => kv.Value == mixerChannelId).Key;
            if (username != null)
            {
                foreach (var connection in _chatServiceAnnounceChannelId)
                {
                    await connection.Key.SendMessageToChannelIdAsync(
                        connection.Value,
                        $"🎥 Someone is streaming! https://mixer.com/{username}");
                }
            }
        }
    }
}