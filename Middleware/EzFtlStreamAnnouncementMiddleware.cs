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
using Theorem.Utility;

namespace Theorem.Middleware
{
    public class EzFtlStreamAnnouncementMiddleware : IMiddleware
    {
        /// <summary>
        /// Logging instance
        /// </summary>
        private ILogger<EzFtlStreamAnnouncementMiddleware> _logger;

        /// <summary>
        /// Configuration object for retrieving configuration values
        /// </summary>
        private ConfigurationSection _configuration;

        /// <summary>
        /// List of chat service connections that we can use to announce streams.
        /// </summary>
        private IEnumerable<IChatServiceConnection> _chatServiceConnections;

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

        private string _hostname
        {
            get
            {
                return _configuration.GetValue<string>("Hostname");
            }
        }

        private string _port
        {
            get
            {
                return _configuration.GetValue<string>("Port");
            }
        }

        public EzFtlStreamAnnouncementMiddleware(ILogger<EzFtlStreamAnnouncementMiddleware> logger,
            ConfigurationSection configuration,
            IEnumerable<IChatServiceConnection> chatServiceConnections)
        {
            _logger = logger;
            _configuration = configuration;
            _chatServiceConnections = chatServiceConnections;

            _logger.LogInformation("Starting EzFtlStreamAnnouncement Middleware...");
            subscribeToChatServiceConnectedEvents();
            TaskUtilities
                .ExpontentialRetryAsync(startEzFtlConnection, onConnectionInterrupted)
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
            _logger.LogError("EzFtl connection threw exception. " + 
                "retry {n} in {s} seconds. Exception: {e}",
                retries.retryNumber,
                retries.nextRetrySeconds,
                exception.Message);
        }

        private async void onChatServiceConnected(object sender, EventArgs e)
        {
            var connection = sender as IChatServiceConnection;
            var matchingService = _configuration.GetSection("AnnounceChannels").GetChildren()
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

        private async Task startEzFtlConnection()
        {
            _logger.LogInformation("Connecting to EzFtl service...");
            var webSocketClient = new ClientWebSocket();
            await webSocketClient.ConnectAsync(
                new Uri($"ws://{_hostname}:{_port}/ws"),
                CancellationToken.None);
            _logger.LogInformation("Connected to EzFtl!");
            await receive(webSocketClient);
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
                    result = await webSocketClient.ReceiveAsync(new ArraySegment<byte>(buffer),
                        CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure,
                            string.Empty, CancellationToken.None);
                    }
                    messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!result.EndOfMessage);
                
                string messageString = messageBuilder.ToString();
                _logger.LogDebug("Received message: {message}", messageString);
                try
                {
                    var channel = JsonConvert.DeserializeObject<ChannelUpdateModel>(messageString);
                    if (channel.HasActiveStream)
                    {
                        await sendStreamingAnnouncement(channel);
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

        private async Task sendStreamingAnnouncement(ChannelUpdateModel channel)
        {
            foreach (var connection in _chatServiceAnnounceChannelId)
            {
                await connection.Key.SendMessageToChannelIdAsync(
                    connection.Value,
                    new ChatMessageModel()
                    {
                        Body = $"🎥 '{channel.ChannelName}' has started! " + 
                            $"http://{_hostname}/{channel.ChannelId}",
                    });
            }
        }

        /* Private Types */
        private class ChannelUpdateModel
        {
            [JsonProperty("channel_id")]
            public int ChannelId { get; set; }

            [JsonProperty("channel_name")]
            public string ChannelName { get; set; }

            [JsonProperty("has_active_stream")]
            public bool HasActiveStream { get; set; }

            [JsonProperty("active_stream_id")]
            public int ActiveStreamId { get; set; }
        }
    }
}