using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Theorem.Converters;
using Theorem.Models;
using Theorem.Models.Slack;
using Theorem.Models.Slack.Events;
using Theorem.Utility;

namespace Theorem.ChatServices
{
    /// <summary>
    /// SlackProvider provides all Slack functionality (send/receive/etc)
    /// </summary>
    public class SlackChatServiceConnection : 
        IChatServiceConnection
    {
        /// <summary>
        /// Base URL for the Slack API
        /// </summary> 
        public const string BaseApiUrl = "https://slack.com/api/";
        
        /// <summary>
        /// Configuration object for retrieving configuration values
        /// </summary>
        private ConfigurationSection _configuration { get; set; }

        /// <summary>
        /// Logger instance for logging events
        /// </summary>
        private ILogger<SlackChatServiceConnection> _logger { get; set; }
        
        /// <summary>
        /// Easy access to the API token via configuration object
        /// </summary>
        private string _apiToken
        {
            get
            {
                return _configuration["ApiToken"];
            }
        }
        
        /// <summary>
        /// Keeps track of the web socket URL to connect Slack via
        /// </summary>
        private string _webSocketUrl { get; set; }
        
        /// <summary>
        /// Information about the authenticated Slack user
        /// </summary>
        public SlackSelfModel Self { get; private set; }

        /// <summary>
        /// User ID assigned to us by Slack
        /// </summary>
        public string UserId
        {
            get
            {
                return Self.Id;
            }
        }

        /// <summary>
        /// User name of the bot in this service
        /// </summary>
        public string UserName
        {
            get
            {
                return Self.Name;
            }
        }

        /// <summary>
        /// Collection of users present on this chat service connection
        /// </summary>
        // TODO: Not implemented
        public ObservableCollection<UserModel> Users { get; private set; }
            = new ObservableCollection<UserModel>();

        /// <summary>
        /// Collection of users currently online on this chat service connection
        /// </summary>
        // TODO: Not implemented
        public ObservableCollection<UserModel> OnlineUsers { get; private set; }
            = new ObservableCollection<UserModel>();
        
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
        /// Event that fires when connected
        /// </summary>
        public event EventHandler<EventArgs> Connected;

        /// <summary>
        /// Event that fires on receipt of a new message
        /// </summary>
        public event EventHandler<ChatMessageModel> NewMessage;
        
        /// <summary>
        /// Constructs a new instance of SlackProvider, requires configuration for things like API token
        /// </summary>
        /// <param name="configuration">Configuration object</param>
        public SlackChatServiceConnection(ConfigurationSection configuration,
            ILogger<SlackChatServiceConnection> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _messageDeserializationSettings = new JsonSerializerSettings();
            _messageDeserializationSettings.Converters.Add(new SlackEventConverter());
        }
        
        /// <summary>
        /// Connects to Slack
        /// </summary>
        public async Task StartAsync()
        {
            _logger.LogInformation("Connecting to Slack instance {name}...", Name);
            using (var httpClient = new HttpClient())
            {
                httpClient.BaseAddress = new Uri(BaseApiUrl);
                var startResult = await httpClient.GetAsync($"rtm.start?token={_apiToken}");
                var startStringResult = await startResult.Content.ReadAsStringAsync();
                var startResponse = JsonConvert.DeserializeObject<SlackStartResponseModel>(startStringResult);
                if (!startResult.IsSuccessStatusCode || !startResponse.Ok)
                {
                    throw new Exception("Failed to open connection via rtm.start."); //TODO: Better error handling.
                }
                
                // Save relevant return information
                _webSocketUrl = startResponse.Url;
                Self = startResponse.Self;
                
                // Connect to websocket endpoint
                var webSocketClient = new ClientWebSocket();
                await webSocketClient.ConnectAsync(new Uri(_webSocketUrl), CancellationToken.None);
                await Task.WhenAll(receive(webSocketClient));
            }
        }
        
        /// <summary>
        /// Processes raw incoming websocket data from Slack
        /// </summary>
        /// <param name="webSocketClient">The web socket client object receiving from</param>
        private async Task receive(ClientWebSocket webSocketClient)
        {
            onConnected();
            while (webSocketClient.State == WebSocketState.Open)
            {
                StringBuilder messageBuilder = new StringBuilder();
                byte[] buffer = new byte[128];
                WebSocketReceiveResult result;
                do
                {
                    result = await webSocketClient.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    }
                    messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!result.EndOfMessage);

                string messageString = messageBuilder.ToString();
                _logger.LogDebug("Received message: {message}", messageString);
                
                var slackEvent = JsonConvert.DeserializeObject<SlackEventModel>(messageString,
                    _messageDeserializationSettings);
                handleSlackEvent(slackEvent);
            }
        }

        /// <summary>
        /// Processes events received from Slack.
        /// </summary>
        /// <param name="slackEvent">The parsed Slack event</param>
        private void handleSlackEvent(SlackEventModel slackEvent)
        {
            if (slackEvent is SlackMessageEventModel)
            {
                var slackMessage = slackEvent as SlackMessageEventModel;
                onNewMessage(slackMessage.ToChatMessageModel(this));
            }
        }
        
        /// <summary>
        /// Send a message to the channel with the given ID
        /// </summary>
        /// <param name="channelSlackId">Channel Slack ID</param>
        /// <param name="body">Body of the message</param>
        public async Task SendMessageToChannelIdAsync(
            string channelId,
            string body)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.BaseAddress = new Uri(BaseApiUrl);
                var postData = new FormUrlEncodedContent(new[] { 
                    new KeyValuePair<string, string>("token", _apiToken), 
                    new KeyValuePair<string, string>("channel", channelId), 
                    new KeyValuePair<string, string>("text", body),
                    new KeyValuePair<string, string>("as_user", "true")
                }); 
                var result = await httpClient.PostAsync("chat.postMessage", postData);
                // TODO: Parse result, handle errors, retry, etc.
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

        public async Task<string> GetChannelIdFromChannelNameAsync(string channelName)
        {
            // TODO
            return "";
        }

        public async Task SetChannelTopicAsync(string channelId, string topic)
        {
            // TODO
        }

        public async Task<int> GetMemberCountFromChannelIdAsync(string channelId)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.BaseAddress = new Uri(BaseApiUrl);
                var postData = new FormUrlEncodedContent(new[] { 
                    new KeyValuePair<string, string>("token", _apiToken), 
                    new KeyValuePair<string, string>("channel", channelId), 
                    new KeyValuePair<string, string>("include_num_members", "true")
                }); 
                var result = await httpClient.PostAsync("conversations.info", postData);

                result.EnsureSuccessStatusCode();

                var responseContent = await result.Content.ReadAsStringAsync();
                var response = JsonConvert.DeserializeObject<SlackConversationInfoModel>(responseContent);

                return response.Channel.MemberCount;
            }
        }
    }
}