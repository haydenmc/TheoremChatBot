using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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
using Theorem.Utility;

namespace Theorem.Middleware
{
    public class GlimeshStreamAnnouncementMiddleware : IMiddleware
    {
        /// <summary>
        /// Logging instance
        /// </summary>
        private ILogger<GlimeshStreamAnnouncementMiddleware> _logger;

        /// <summary>
        /// Configuration object for retrieving configuration values
        /// </summary>
        private ConfigurationSection _configuration;

        /// <summary>
        /// List of chat service connections that we can use to announce streams.
        /// </summary>
        private IEnumerable<IChatServiceConnection> _chatServiceConnections;

        /// <summary>
        /// Hostname of the Glimesh service
        /// </summary>
        private const string _hostname = "glimesh.tv";

        /// <summary>
        /// Client ID used to authenticate.
        /// </summary>
        private string _clientId
        {
            get
            {
                return _configuration["ClientId"];
            }
        }

        /// <summary>
        /// Client Secret used to authenticate.
        /// </summary>
        private string _clientSecret
        {
            get
            {
                return _configuration["ClientSecret"];
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
                    .GetSection("GlimeshUsernames")
                    .GetChildren()
                    .Select(c => c.Value);
            }
        }

        /// <summary>
        /// Mapping of Glimesh usernames to respective channel IDs
        /// </summary>
        private Dictionary<string, string> _glimeshUsernameChannelIdMap
            = new Dictionary<string, string>();

        /// <summary>
        /// Contains a set of channel IDs that are already marked as streaming
        /// </summary>
        private HashSet<string> _currentlyStreamingChannelIds =
            new HashSet<string>();

        /// <summary>
        /// Tracks channels for each chat service instance where we should announce streams.
        /// </summary>
        private Dictionary<IChatServiceConnection, string> _chatServiceAnnounceChannelId
            = new Dictionary<IChatServiceConnection, string>();

        public GlimeshStreamAnnouncementMiddleware(
            ILogger<GlimeshStreamAnnouncementMiddleware> logger,
            ConfigurationSection configuration,
            IEnumerable<IChatServiceConnection> chatServiceConnections)
        {
            _logger = logger;
            _configuration = configuration;
            _chatServiceConnections = chatServiceConnections;

            _logger.LogInformation("Starting GlimeshStreamAnnouncement Middleware...");
            subscribeToChatServiceConnectedEvents();

            TaskUtilities
                .ExpontentialRetryAsync(startGlimeshConnection, onConnectionInterrupted)
                .FireAndForget();
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

        private void onConnectionInterrupted(Exception exception,
            (uint retryNumber, uint nextRetrySeconds) retries)
        {
            _logger.LogError("Glimesh connection threw exception. " +
                "retry {n} in {s} seconds. Exception: {e}",
                retries.retryNumber,
                retries.nextRetrySeconds,
                exception.Message);
        }

        private async void onChatServiceConnected(object sender, EventArgs e)
        {
            _logger.LogInformation("onChatServiceConnected");
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

        private async Task startGlimeshConnection()
        {
            var tokenResponse = await requestAccessToken();

            _logger.LogInformation("Looking up Channel IDs to subscribe to...");
            foreach (var username in _usernames)
            {
                string channelId = await getChannelIdFromUsername(username, tokenResponse);
                if (channelId != null)
                {
                    _logger.LogInformation("Found Glimesh channel ID {id} for user {user}",
                        channelId,
                        username);
                    _glimeshUsernameChannelIdMap[username] = channelId;
                }
            }

            _logger.LogDebug("Connecting to Glimesh Websocket API...");

            // Connect to websocket endpoint, currently we stay connected even after the access token expires
            var uri = new Uri($"wss://{_hostname}/api/socket/websocket?vsn=2.0.0&token={tokenResponse.access_token}");
            var socket = new ClientWebSocket();
            await socket.ConnectAsync(uri, CancellationToken.None);

            // Join some kind of control channel, required by Phoneix backend
            await sendMessage(socket, "__absinthe__:control", "phx_join", new { });

            // Subscribe to channel status updates for all the users we care about
            foreach (var channelId in _glimeshUsernameChannelIdMap.Values)
            {
                await subscribeToChannelStatus(socket, channelId);
            }

            _logger.LogDebug("Connected to Glimesh Websocket API!");
            await receive(socket);
        }

        private async Task subscribeToChannelStatus(ClientWebSocket socket, string channelId)
        {
            await sendMessage(socket, "__absinthe__:control", "doc", new
            {
                query = @"subscription($channelId: ID) {
                        channel(id: $channelId) {
                            id
                            title
                            status
                        }
                    }",
                variables = new
                {
                    channelId,
                },
            });
        }

        private Task sendMessage(ClientWebSocket socket, string channel, string operation, object payload)
        {
            var message = JsonConvert.SerializeObject(new object[] { "1", "1", channel, operation, payload });
            return socket.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task<TokenResponse> requestAccessToken()
        {
            using (var httpClient = new HttpClient())
            {
                var body = JsonConvert.SerializeObject(new
                {
                    grant_type = "client_credentials",
                    client_id = _clientId,
                    client_secret = _clientSecret,
                });
                var result = await httpClient.PostAsync(
                    $"https://{_hostname}/api/oauth/token",
                    new StringContent(body, Encoding.UTF8, "application/json"));

                result.EnsureSuccessStatusCode();

                var responseContent = await result.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<TokenResponse>(responseContent);
            }
        }
        private class TokenResponse
        {
            public string access_token { get; set; }
        }

        private async Task<string> getChannelIdFromUsername(string username, TokenResponse tokenResponse)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenResponse.access_token);
                var body = JsonConvert.SerializeObject(new
                {
                    query = @"query($username: String) {
                        user(username: $username) {
                            id
                            username
                            channel {
                                id
                            }
                        }
                    }",
                    variables = new
                    {
                        username,
                    },
                });
                var result = await httpClient.PostAsync(
                    $"https://{_hostname}/api/graph",
                    new StringContent(body, Encoding.UTF8, "application/json"));

                result.EnsureSuccessStatusCode();

                var responseContent = await result.Content.ReadAsStringAsync();
                var response = JsonConvert.DeserializeObject<UserQueryResponse>(responseContent);
                return response.data.user.channel.id;
            }
        }

        private class UserQueryResponse
        {
            public Data data { get; set; }

            public class Data
            {
                public User user { get; set; }

                public class User
                {
                    public String id { get; set; }
                    public String username { get; set; }
                    public Channel channel { get; set; }
                    public class Channel
                    {
                        public String id { get; set; }
                    }
                }
            }
        }


        /// <summary>
        /// Processes raw incoming websocket data from Glimesh
        /// </summary>
        /// <param name="socket">
        /// The web socket client object receiving from
        /// </param>
        private async Task receive(ClientWebSocket socket)
        {
            while (socket.State == WebSocketState.Open)
            {
                StringBuilder messageBuilder = new StringBuilder();
                byte[] buffer = new byte[1024];
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(
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
                    var parsedMessage = JArray.Parse(messageString);

                    var channel = parsedMessage[2].ToObject<string>();
                    var operation = parsedMessage[3].ToObject<string>();
                    var body = parsedMessage[4];

                    // TODO Check for OK response on other messages

                    if (operation == "subscription:data")
                    {
                        var channelId = body.SelectToken("result.data.channel.id").ToObject<string>();
                        var channelStatus = body.SelectToken("result.data.channel.status").ToObject<string>();
                        var channelTitle = body.SelectToken("result.data.channel.title").ToObject<string>();
                        var isLive = channelStatus == "LIVE";
                        if (isLive)
                        {
                            if (!_currentlyStreamingChannelIds.Contains(channelId))
                            {
                                _currentlyStreamingChannelIds.Add(channelId);
                                await sendStreamingAnnouncement(channelId, channelTitle);
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
                    _logger.LogError("Could not process message from Glimesh: {message}\n{e}",
                        messageString,
                        e.Message);
                }
            }
        }

        private async Task sendStreamingAnnouncement(string channelId, string channelTitle)
        {
            var username = _glimeshUsernameChannelIdMap
                .SingleOrDefault(kv => kv.Value == channelId).Key;
            if (username != null)
            {
                foreach (var connection in _chatServiceAnnounceChannelId)
                {
                    await connection.Key.SendMessageToChannelIdAsync(
                        connection.Value,
                        new ChatMessageModel()
                        {
                            Body = $"🎥 '{username}' is streaming '{channelTitle}': https://{_hostname}/{username}",
                        });
                }
            }
        }
    }
}