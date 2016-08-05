using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Theorem.Converters;
using Theorem.Models;
using Theorem.Models.Events;
using Theorem.Models.Slack;

namespace Theorem.Providers
{
    /// <summary>
    /// SlackProvider provides all Slack functionality (send/receive/etc)
    /// </summary>
    public class SlackProvider
    {
        /// <summary>
        /// Base URL for the Slack API
        /// </summary> 
        public const string BaseApiUrl = "https://slack.com/api/";
        
        /// <summary>
        /// Configuration object for retrieving configuration values
        /// </summary>
        private IConfigurationRoot _configuration { get; set; }
        
        /// <summary>
        /// Easy access to the API token via configuration object
        /// </summary>
        private string _apiToken
        {
            get
            {
                return _configuration["Slack:ApiToken"];
            }
        }
        
        /// <summary>
        /// Returns a new db context to use for interacting with the database
        /// </summary>
        private Func<ApplicationDbContext> _dbContext { get; set; }
        
        /// <summary>
        /// Keeps track of the web socket URL to connect Slack via
        /// </summary>
        private string _webSocketUrl { get; set; }
        
        /// <summary>
        /// Dictionary that keeps track of available channels, keyed on channel id (NOT name)
        /// </summary>
        public Dictionary<string, SlackChannelModel> ChannelsById { get; private set; }
        
        /// <summary>
        /// Dictionary that keeps track of users, keyed on user id (NOT name)
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, SlackUserModel> UsersById { get; private set; }
        
        /// <summary>
        /// Information about the authenticated Slack user
        /// </summary>
        public SlackSelfModel Self { get; private set; }
        
        private JsonSerializerSettings _messageDeserializationSettings { get; set; }
        
        /// <summary>
        /// Event that fires on receipt of a new message
        /// </summary>
        public event EventHandler<MessageEventModel> NewMessage;
        
        /// <summary>
        /// Constructs a new instance of SlackProvider, requires configuration for things like API token
        /// </summary>
        /// <param name="configuration">Configuration object</param>
        public SlackProvider(IConfigurationRoot configuration, Func<ApplicationDbContext> dbContext)
        {
            _configuration = configuration;
            _dbContext = dbContext;
            _messageDeserializationSettings = new JsonSerializerSettings();
            _messageDeserializationSettings.Converters.Add(new SlackEventConverter());
        }
        
        /// <summary>
        /// Connects to Slack
        /// </summary>
        public async Task Connect()
        {
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
                ChannelsById = startResponse.Channels.ToDictionary(c => c.Id);
                UsersById = startResponse.Users.ToDictionary(u => u.Id);
                Self = startResponse.Self;
                
                // Commit user and channel information to database
                using (var db = _dbContext())
                {
                    foreach (var user in startResponse.Users)
                    {
                        var dbUser = db.Users.SingleOrDefault(u => u.SlackId == user.Id);
                        if (dbUser == null)
                        {
                            dbUser = user.ToUserModel();
                            dbUser.Id = Guid.NewGuid();
                            db.Users.Add(dbUser);
                            await db.SaveChangesAsync();
                        }
                        else
                        {
                            // TODO: Update the database model with any changed fields
                        }
                    }
                    foreach (var channel in startResponse.Channels)
                    {
                        var dbChannel = db.Channels.SingleOrDefault(c => c.SlackId == channel.Id);
                        if (dbChannel == null)
                        {
                            dbChannel = channel.ToChannelModel();
                            dbChannel.Id = Guid.NewGuid();
                            dbChannel.Creator = db.Users.SingleOrDefault(u => u.SlackId == dbChannel.CreatorSlackId);
                            db.Channels.Add(dbChannel);
                            await db.SaveChangesAsync();
                        }
                        else
                        {
                            // TODO: Update the database model with any changed fields
                        }
                    }
                }
                
                // Connect to websocket endpoint
                var webSocketClient = new ClientWebSocket();
                await webSocketClient.ConnectAsync(new Uri(_webSocketUrl), CancellationToken.None);
                await Task.WhenAll(Receive(webSocketClient));
            }
        }
        
        /// <summary>
        /// Processes raw incoming websocket data from Slack
        /// </summary>
        /// <param name="webSocketClient">The web socket client object receiving from</param>
        private async Task Receive(ClientWebSocket webSocketClient)
        {
            while (webSocketClient.State == WebSocketState.Open)
            {
                StringBuilder messageString = new StringBuilder();
                byte[] buffer = new byte[1024];
                WebSocketReceiveResult result;
                do
                {
                    result = await webSocketClient.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    }
                    messageString.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!result.EndOfMessage);
                
                var slackEvent = JsonConvert.DeserializeObject<EventModel>(messageString.ToString(), _messageDeserializationSettings);
                Console.WriteLine(messageString.ToString());
                await HandleSlackEvent(slackEvent);
            }
        }

        /// <summary>
        /// Processes events received from Slack.
        /// </summary>
        /// <param name="slackEvent">The parsed Slack event</param>
        private async Task HandleSlackEvent(EventModel slackEvent)
        {
            if (slackEvent is MessageEventModel)
            {
                using (var db = _dbContext())
                {
                    var dbMessage = (MessageEventModel)slackEvent;
                    dbMessage.Id = Guid.NewGuid();
                    dbMessage.Channel = db.Channels.SingleOrDefault(c => c.SlackId == dbMessage.SlackChannelId);
                    dbMessage.User = db.Users.SingleOrDefault(u => u.SlackId == dbMessage.SlackUserId);
                    db.MessageEvents.Add(dbMessage);
                    await db.SaveChangesAsync();
                    OnNewMessage((MessageEventModel)slackEvent);
                }
            }
            else if (slackEvent is PresenceChangeEventModel)
            {
                using (var db = _dbContext())
                {
                    var dbPresenceChangeEvent = (PresenceChangeEventModel)slackEvent;
                    dbPresenceChangeEvent.Id = Guid.NewGuid();
                    dbPresenceChangeEvent.Channel = db.Channels.SingleOrDefault(c => c.SlackId == dbPresenceChangeEvent.SlackChannelId);
                    dbPresenceChangeEvent.User = db.Users.SingleOrDefault(u => u.SlackId == dbPresenceChangeEvent.SlackUserId);
                    db.PresenceChangeEvents.Add(dbPresenceChangeEvent);
                    await db.SaveChangesAsync();
                }
            }
            else if (slackEvent is TypingEventModel)
            {
                using (var db = _dbContext())
                {
                    var dbTypingEvent = (TypingEventModel)slackEvent;
                    dbTypingEvent.Id = Guid.NewGuid();
                    dbTypingEvent.Channel = db.Channels.SingleOrDefault(c => c.SlackId == dbTypingEvent.SlackChannelId);
                    dbTypingEvent.User = db.Users.SingleOrDefault(u => u.SlackId == dbTypingEvent.SlackUserId);
                    db.TypingEvents.Add(dbTypingEvent);
                    await db.SaveChangesAsync();
                }
            }
        }
        
        /// <summary>
        /// Send a message to the channel with the given name
        /// </summary>
        /// <param name="channelName">Channel name</param>
        /// <param name="body">Body of the message</param>
        /// <returns></returns>
        public async Task SendMessageToChannelName(string channelName, string body)
        {
            var targetChannel =
                ChannelsById.Values
                .SingleOrDefault(c => c.Name.ToUpperInvariant() == channelName.ToUpperInvariant());
            if (targetChannel != null)
            {
                await SendMessageToChannelId(targetChannel.Id, body);
            }
        }
        
        /// <summary>
        /// Send a message to the channel with the given ID
        /// </summary>
        /// <param name="channelId">Channel ID</param>
        /// <param name="body">Body of the message</param>
        public async Task SendMessageToChannelId(string channelId, string body)
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
        /// Used to raise the NewMessage event.
        /// </summary>
        /// <param name="message">Event arguments; the message that was received</param>
        protected virtual void OnNewMessage(MessageEventModel message)
        {
            var eventHandler = NewMessage;
            if (eventHandler != null)
            {
                eventHandler(this, message);
            }
        }
    }
}