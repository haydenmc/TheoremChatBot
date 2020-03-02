using System;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Theorem.Converters;
using Theorem.Models.Events;
using Theorem.Models.Mattermost;
using Theorem.Models.Mattermost.EventData;

namespace Theorem.Providers
{
    /// <summary>
    /// MattermostProvider enables connection to Mattermost server.
    /// </summary>
    public class MattermostProvider : 
        IChatProvider
    {
        /// <summary>
        /// Configuration object for retrieving configuration values
        /// </summary>
        private IConfigurationRoot _configuration { get; set; }

        private JsonSerializerSettings _messageDeserializationSettings { get; set; }

        /// <summary>
        /// URL of the server to connect to defined by configuration values
        /// </summary>
        private string _baseWebsocketUrl
        {
            get
            {
                return _configuration["Mattermost:WebsocketServerUrl"];
            }
        }
        
        /// <summary>
        /// Easy access to the access token via configuration object
        /// </summary>
        private string _accessToken
        {
            get
            {
                return _configuration["Mattermost:AccessToken"];
            }
        }

        /// <summary>
        /// Indicates whether or not the websocket connection has been
        /// successfully authenticated or not.
        /// </summary>
        private bool _connectionAuthenticated = false;

        /// <summary>
        /// Event that fires when connected
        /// </summary>
        public event EventHandler<EventArgs> Connected;

        /// <summary>
        /// Event that fires on receipt of a new message
        /// </summary>
        public event EventHandler<MessageEventModel> NewMessage;

        /// <summary>
        /// Constructs a new instance of MattermostProvider, requires
        /// configuration for things like API token
        /// </summary>
        /// <param name="configuration">Configuration object</param>
        public MattermostProvider(IConfigurationRoot configuration)
        {
            _configuration = configuration;
            _messageDeserializationSettings = new JsonSerializerSettings();
            _messageDeserializationSettings.Converters.Add(
                new MattermostEventConverter());
        }

        /// <summary>
        /// Connects to Mattermost
        /// </summary>
        public async Task Connect()
        {
            // Connect to websocket endpoint
            var webSocketClient = new ClientWebSocket();
            await webSocketClient.ConnectAsync(
                new Uri(new Uri(_baseWebsocketUrl), "api/v4/websocket"),
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
            await webSocketClient.SendAsync(
                        Encoding.UTF8.GetBytes(authChallengePayload),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);
            await Task.WhenAll(new Task[]
                {
                    receive(webSocketClient),
                    
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
            onConnected();
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

                Console.WriteLine(messageString);

                if (!_connectionAuthenticated)
                {
                    var authResponse = 
                        JsonConvert.DeserializeObject<MattermostAuthResponseModel>(
                            messageString);
                    if (authResponse.Status == "OK")
                    {
                        _connectionAuthenticated = true;
                    }
                }
                else
                {
                    IMattermostWebsocketMessageModel message = 
                        JsonConvert.DeserializeObject
                            <IMattermostWebsocketMessageModel>(
                                messageString,
                                _messageDeserializationSettings);
                    handleMessage(message);
                }
            }
        }

        private void handleMessage(
            IMattermostWebsocketMessageModel message)
        {
            Type messageType = message.GetType().GetGenericArguments()[0];
            if (messageType == typeof(MattermostHelloEventDataModel))
            {
                Console.WriteLine("Got hello'ed!");
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

        public Task SendMessageToChannelId(string channelId, string body)
        {
            throw new NotImplementedException();
        }
    }
}