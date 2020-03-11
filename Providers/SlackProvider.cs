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
using Theorem.Models.Slack.Events;

namespace Theorem.Providers
{
    /// <summary>
    /// SlackProvider provides all Slack functionality (send/receive/etc)
    /// </summary>
    public class SlackProvider : 
        IChatProvider
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
        public event EventHandler<ChatMessageModel> NewMessage;
        
        /// <summary>
        /// Constructs a new instance of SlackProvider, requires configuration for things like API token
        /// </summary>
        /// <param name="configuration">Configuration object</param>
        public SlackProvider(IConfigurationRoot configuration)
        {
            _configuration = configuration;
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
                byte[] buffer = new byte[128];
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
                
                var slackEvent = JsonConvert.DeserializeObject<SlackEventModel>(messageString.ToString(), _messageDeserializationSettings);
                Console.WriteLine(messageString.ToString());
                await handleSlackEvent(slackEvent);
            }
        }

        /// <summary>
        /// Processes events received from Slack.
        /// </summary>
        /// <param name="slackEvent">The parsed Slack event</param>
        private async Task handleSlackEvent(SlackEventModel slackEvent)
        {
            if (slackEvent is SlackMessageEventModel)
            {
                var slackMessage = (SlackMessageEventModel)slackEvent;
                
                db.MessageEvents.Add(dbMessage);
                await db.SaveChangesAsync();
                onNewMessage((MessageEventModel)slackEvent);
            }
        }
        
        /// <summary>
        /// Send a message to the channel with the given ID
        /// </summary>
        /// <param name="channelSlackId">Channel Slack ID</param>
        /// <param name="body">Body of the message</param>
        public async Task SendMessageToChannelId(
            string channelSlackId,
            string body,
            List<SlackAttachmentModel> attachments)
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

        public async Task SendMessageToChannelId(
            string channelSlackId,
            string body)
        {
            await SendMessageToChannelId(channelSlackId, body, null);
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