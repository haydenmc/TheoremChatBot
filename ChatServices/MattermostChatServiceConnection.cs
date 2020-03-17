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
using Theorem.Converters;
using Theorem.Models;
using Theorem.Models.Mattermost;
using Theorem.Models.Mattermost.EventData;

namespace Theorem.ChatServices
{
    /// <summary>
    /// MattermostProvider enables connection to Mattermost server.
    /// </summary>
    public class MattermostChatServiceConnection : 
        IChatServiceConnection
    {
        /// <summary>
        /// Configuration object for retrieving configuration values
        /// </summary>
        private ConfigurationSection _configuration { get; set; }

        /// <summary>
        /// Logger instance for logging events
        /// </summary>
        private ILogger<MattermostChatServiceConnection> _logger { get; set; }

        /// <summary>
        /// Settings used to deserialize incoming events into the proper types.
        /// </summary>
        private JsonSerializerSettings _messageDeserializationSettings { get; set; }

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
        /// User ID assigned to us by Mattermost
        /// </summary>
        public string UserId
        {
            get
            {
                return _user.Id;
            }
        }

        /// <summary>
        /// URL of the server to connect to defined by configuration values
        /// </summary>
        private string _serverHostname
        {
            get
            {
                return _configuration["ServerHostname"];
            }
        }
        
        /// <summary>
        /// Easy access to the access token via configuration object
        /// </summary>
        private string _accessToken
        {
            get
            {
                return _configuration["AccessToken"];
            }
        }

        /// <summary>
        /// Indicates whether or not the websocket connection has been
        /// successfully authenticated or not.
        /// </summary>
        private bool _connectionAuthenticated = false;

        /// <summary>
        /// Mattermost user model for our bot user
        /// </summary>
        private MattermostUserModel _user;

        /// <summary>
        /// A list of teams we are a member of.
        /// </summary>
        private List<MattermostTeamModel> _teams = new List<MattermostTeamModel>();

        /// <summary>
        /// Event that fires when connected
        /// </summary>
        public event EventHandler<EventArgs> Connected;

        /// <summary>
        /// Event that fires on receipt of a new message
        /// </summary>
        public event EventHandler<ChatMessageModel> NewMessage;

        /// <summary>
        /// Constructs a new instance of MattermostProvider, requires
        /// configuration for things like API token
        /// </summary>
        /// <param name="configuration">Configuration object</param>
        public MattermostChatServiceConnection(ConfigurationSection configuration,
            ILogger<MattermostChatServiceConnection> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _messageDeserializationSettings = new JsonSerializerSettings();
            _messageDeserializationSettings.Converters.Add(
                new MattermostEventConverter());
        }

        /// <summary>
        /// Connects to Mattermost
        /// </summary>
        public async Task StartAsync()
        {
            _logger.LogInformation("Connecting to Mattermost server {server}...",
                _serverHostname);

            // Pull user info
            await populateUserInfoAsync();

            // Pull team information
            await populateTeamsInfoAsync();

            // Connect to websocket endpoint
            var webSocketClient = new ClientWebSocket();
            await webSocketClient.ConnectAsync(
                new Uri(new Uri($"wss://{_serverHostname}"), "api/v4/websocket"),
                CancellationToken.None);

            // Immediately send auth payload, then wait for responses.
            var authChallenge = 
                new MattermostAuthChallengeModel()
                {
                    Seq = 1,
                    Action = "authentication_challenge",
                    Data = new MattermostAuthChallengeModel.AuthChallengeData()
                    {
                        Token = _accessToken
                    }
                };
            var authChallengePayload = JsonConvert.SerializeObject(authChallenge);
            await Task.WhenAll(new Task[]
                {
                    receive(webSocketClient),
                    webSocketClient.SendAsync(
                        Encoding.UTF8.GetBytes(authChallengePayload),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None)
                });
        }

        
        /// <summary>
        /// Processes raw incoming websocket data from Mattermost
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

                if (!_connectionAuthenticated)
                {
                    // Authentication responses are different than usual messages
                    var messageParsed = JObject.Parse(messageString);
                    if (messageParsed.ContainsKey("status"))
                    {
                        var authResponse = 
                            JsonConvert.DeserializeObject<MattermostAuthResponseModel>(
                                messageString);
                        if (authResponse.Status == "OK")
                        {
                            _logger.LogInformation("Mattermost connection authenticated successfully.");
                            _connectionAuthenticated = true;
                        }
                    }
                }

                IMattermostWebsocketMessageModel message = 
                    JsonConvert.DeserializeObject
                        <IMattermostWebsocketMessageModel>(
                            messageString,
                            _messageDeserializationSettings);
                handleMessage(message);
            }
        }

        private void handleMessage(
            IMattermostWebsocketMessageModel message)
        {
            Type messageType = message.GetType().GetGenericArguments()[0];
            if (messageType == typeof(MattermostHelloEventDataModel))
            {
                _logger.LogDebug("Received 'hello' message. Our user ID is '{userid}'.",
                    message.Broadcast.UserId);
                onConnected();
            }
            else if (messageType == typeof(MattermostPostedEventDataModel))
            {
                var postedMessage = message.Data as MattermostPostedEventDataModel;
                _logger.LogDebug("Got message {id}", postedMessage.Post.Id);
                onNewMessage(postedMessage.ToChatMessageModel(this));
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

        /// <summary>
        /// Returns an http client prepared with the correct base URL and authorization headers.
        /// </summary>
        private HttpClient getHttpClient()
        {
            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri($"https://{_serverHostname}");
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _accessToken);
            return httpClient;
        }

        private async Task populateUserInfoAsync()
        {
            using (var httpClient = getHttpClient())
            {
                var result = await httpClient.GetAsync("api/v4/users/me");
                if (!result.IsSuccessStatusCode)
                {
                    _logger.LogError("Encountered error code {code} calling {uri}: {msg}",
                        result.StatusCode,
                        result.Headers.Location,
                        result.ReasonPhrase);
                    return;
                }
                else
                {
                    var content = await result.Content.ReadAsStringAsync();
                    _user = JsonConvert.DeserializeObject<MattermostUserModel>(content);
                    _logger.LogInformation("Received Mattermost user info - {id}: {name}",
                        _user.Id,
                        _user.Username);
                    return;
                }
            }
        }

        private async Task populateTeamsInfoAsync()
        {
            using (var httpClient = getHttpClient())
            {
                var result = await httpClient.GetAsync("api/v4/users/me/teams");
                if (!result.IsSuccessStatusCode)
                {
                    _logger.LogError("Encountered error code {code} calling {uri}: {msg}",
                        result.StatusCode,
                        result.Headers.Location,
                        result.ReasonPhrase);
                    return;
                }
                else
                {
                    var content = await result.Content.ReadAsStringAsync();
                    _teams = JsonConvert.DeserializeObject<List<MattermostTeamModel>>(content);
                    _logger.LogInformation("Received Mattermost team info: {teams}",
                        String.Join(", ", _teams.Select(t => $"{t.Id}: {t.DisplayName}")));
                    return;
                }
            }
        }

        /// <summary>
        /// Send a message to the channel with the given ID
        /// </summary>
        /// <param name="channelSlackId">Channel ID</param>
        /// <param name="body">Body of the message</param>
        public async Task SendMessageToChannelIdAsync(string channelId, string body)
        {
            using (var httpClient = getHttpClient())
            {
                var messageObject = new {
                    channel_id = channelId,
                    message = body
                };
                var messageString = JsonConvert.SerializeObject(messageObject);
                _logger.LogDebug("Sending message to channel: {messagePayload}", messageString);
                var content = new StringContent(
                    messageString,
                    Encoding.UTF8,
                    "application/json");
                var result = await httpClient.PostAsync("api/v4/posts", content);
                // TODO: Parse result, handle errors, retry, etc.
            }
        }

        public async Task<string> GetChannelIdFromChannelNameAsync(string channelName)
        {
            if (_teams.Count <= 0)
            {
                _logger.LogError("Couldn't query channel IDs - no teams present.");
                return "";
            }
            var teamId = _teams.First().Id;
            using (var httpClient = getHttpClient())
            {
                var result = await httpClient.GetAsync($"api/v4/teams/{teamId}/channels/name/{channelName}");
                if (!result.IsSuccessStatusCode)
                {
                    _logger.LogError("Encountered error code {code} calling {uri}: {msg}",
                        result.StatusCode,
                        result.Headers.Location,
                        result.ReasonPhrase);
                    return "";
                }
                else
                {
                    var content = await result.Content.ReadAsStringAsync();
                    var parsedContent = JObject.Parse(content);
                    if (parsedContent.ContainsKey("id"))
                    {
                        return parsedContent.GetValue("id").ToObject<string>();
                    }
                    else
                    {
                        _logger.LogError(
                            "Could not find 'id' value in channel query response: {response}",
                            content);
                        return "";
                    }
                }
            }
        }
    }
}