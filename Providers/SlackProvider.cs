using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
        /// Dictionary of instant messages, keyed by id
        /// </summary>
        public Dictionary<string, SlackImModel> ImsById { get; private set; }
        
        /// <summary>
        /// Information about the authenticated Slack user
        /// </summary>
        public SlackSelfModel Self { get; private set; }
        
        private JsonSerializerSettings _messageDeserializationSettings { get; set; }
        
        /// <summary>
        /// Event that fires when connected
        /// </summary>
        public event EventHandler<EventArgs> Connected;

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
                ImsById = startResponse.Ims.ToDictionary(i => i.Id);
                Self = startResponse.Self;
                
                // Commit user and channel information to database
                using (var db = _dbContext())
                {
                    foreach (var user in startResponse.Users)
                    {
                        db.AddOrUpdateDbUser(user);
                    }
                    foreach (var channel in startResponse.Channels)
                    {
                        db.AddOrUpdateDbChannel(channel);
                    }
                }
                
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
                await handleSlackEvent(slackEvent);
            }
        }

        /// <summary>
        /// Processes events received from Slack.
        /// </summary>
        /// <param name="slackEvent">The parsed Slack event</param>
        private async Task handleSlackEvent(EventModel slackEvent)
        {
            using (var db = _dbContext())
            {
                // Populate generic fields from database
                slackEvent.Id = Guid.NewGuid();
                slackEvent.Channel = db.Channels.SingleOrDefault(c => c.SlackId == slackEvent.SlackChannelId);
                slackEvent.User = db.Users.SingleOrDefault(u => u.SlackId == slackEvent.SlackUserId);
                slackEvent.TimeReceived = DateTimeOffset.Now;

                if (slackEvent is ChannelCreatedEventModel)
                {
                    var dbChannelCreated = (ChannelCreatedEventModel)slackEvent;
                    db.ChannelCreatedEvents.Add(dbChannelCreated);
                    await db.SaveChangesAsync();
                }
                else if (slackEvent is ChannelJoinedEventModel)
                {
                    var dbChannelJoined = (ChannelJoinedEventModel)slackEvent;
                    // Add or update this channel
                    dbChannelJoined.ChannelId = db.AddOrUpdateDbChannel(dbChannelJoined.SlackChannel).Id;
                    db.ChannelJoinedEvents.Add(dbChannelJoined);
                    await db.SaveChangesAsync();
                }
                else if (slackEvent is MessageEventModel)
                {
                    var dbMessage = (MessageEventModel)slackEvent;
                    db.MessageEvents.Add(dbMessage);
                    await db.SaveChangesAsync();
                    onNewMessage((MessageEventModel)slackEvent);
                }
                else if (slackEvent is PresenceChangeEventModel)
                {
                    db.PresenceChangeEvents.Add(slackEvent as PresenceChangeEventModel);
                    await db.SaveChangesAsync();
                }
                else if (slackEvent is TypingEventModel)
                {
                    db.TypingEvents.Add(slackEvent as TypingEventModel);
                    await db.SaveChangesAsync();
                }
                else
                {
                    // Generic events
                    db.Events.Add(slackEvent);
                    await db.SaveChangesAsync();
                }
            }
        }

        /// <summary>
        /// Queries the database for a user with the specified Slack ID
        /// </summary>
        /// <param name="slackUserId">Slack ID of the requested user</param>
        /// <returns>Databse model of Slack user</returns>
        public UserModel GetUserBySlackId(string slackUserId)
        {
            using (var db = _dbContext())
            {
                return db.Users.AsNoTracking().SingleOrDefault(u => u.SlackId == slackUserId);
            }
        }

        /// <summary>
        /// Queries the database for a channel with the specified Slack ID
        /// </summary>
        /// <param name="slackId">Slack ID of the requested channel</param>
        /// <returns>Database model of Slack channel</returns>
        public ChannelModel GetChannelBySlackId(string slackId)
        {
            using (var db = _dbContext())
            {
                return db.Channels.AsNoTracking().SingleOrDefault(c => c.SlackId == slackId);
            }
        }

        /// <summary>
        /// Queries the database for a channel with the specified name 
        /// </summary>
        /// <param name="channelName">Name of requested channel</param>
        /// <returns>Database model of the Slack channel</returns>
        public ChannelModel GetChannelByName(string channelName)
        {
            using (var db = _dbContext())
            {
                return db.Channels.AsNoTracking().SingleOrDefault(c => c.Name.ToUpper() == channelName.ToUpper());
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
            var targetChannel = GetChannelByName(channelName);
            if (targetChannel != null)
            {
                await SendMessageToChannelId(targetChannel.SlackId, body);
            }
        }
        
        /// <summary>
        /// Send a message to the channel with the given ID
        /// </summary>
        /// <param name="channelSlackId">Channel Slack ID</param>
        /// <param name="body">Body of the message</param>
        public async Task SendMessageToChannelId(string channelSlackId, string body, List<SlackAttachmentModel> attachments = null)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.BaseAddress = new Uri(BaseApiUrl);
                var attachmentsStr = "";
                if (attachments != null)
                {
                    attachmentsStr = JsonConvert.SerializeObject(attachments);
                }
                var postData = new FormUrlEncodedContent(new[] { 
                    new KeyValuePair<string, string>("token", _apiToken), 
                    new KeyValuePair<string, string>("channel", channelSlackId), 
                    new KeyValuePair<string, string>("text", body),
                    new KeyValuePair<string, string>("as_user", "true"),
                    new KeyValuePair<string, string>("attachments", attachmentsStr)
                }); 
                var result = await httpClient.PostAsync("chat.postMessage", postData);
                // TODO: Parse result, handle errors, retry, etc.
            }
        }

        /// <summary>
        /// React to the given message
        /// </summary>
        /// <param name="reaction">Reaction identifier</param>
        /// <param name="message">The message to react to</param>
        public async Task ReactToMessage(string reaction, MessageEventModel message)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.BaseAddress = new Uri(BaseApiUrl);
                var postData = new FormUrlEncodedContent(new [] { 
                    new KeyValuePair<string, string>("token", _apiToken), 
                    new KeyValuePair<string, string>("channel", message.Channel.SlackId),
                    new KeyValuePair<string, string>("timestamp", message.SlackTimeSent.ToString()), 
                    new KeyValuePair<string, string>("name", reaction)
                }); 
                var result = await httpClient.PostAsync("reactions.add", postData);
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
        protected virtual void onNewMessage(MessageEventModel message)
        {
            var eventHandler = NewMessage;
            if (eventHandler != null)
            {
                eventHandler(this, message);
            }
        }
    }
}