using System;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Theorem.Models.Mattermost;

namespace Theorem.Providers
{
    /// <summary>
    /// MattermostProvider enables connection to Mattermost server.
    /// </summary>
    public class MattermostProvider
    {
        /// <summary>
        /// Configuration object for retrieving configuration values
        /// </summary>
        private IConfigurationRoot _configuration { get; set; }

        /// <summary>
        /// URL of the server to connect to defined by configuration values
        /// </summary>
        private string _baseUrl
        {
            get
            {
                return _configuration["Mattermost:ServerUrl"];
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
        /// Constructs a new instance of MattermostProvider, requires
        /// configuration for things like API token
        /// </summary>
        /// <param name="configuration">Configuration object</param>
        public MattermostProvider(IConfigurationRoot configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Connects to Mattermost
        /// </summary>
        public async Task Connect()
        {
            // Connect to websocket endpoint
            var webSocketClient = new ClientWebSocket();
            await webSocketClient.ConnectAsync(
                new Uri(new Uri(_baseUrl), "api/v4/websocket"),
                CancellationToken.None);

            // Immediately send auth payload, then wait for responses.
            var authChallenge = 
                new MattermostWebsocketMessageModel<MattermostAuthChallengeDataModel>()
                {
                    Seq = 1,
                    Action = "authentication_challenge",
                    Data = new MattermostAuthChallengeDataModel()
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
                        false,
                        CancellationToken.None)
                });
        }

        
        /// <summary>
        /// Processes raw incoming websocket data from Mattermost
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
    }
}