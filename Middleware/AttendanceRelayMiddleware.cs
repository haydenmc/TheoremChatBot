using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Theorem.ChatServices;
using Theorem.Models;

namespace Theorem.Middleware
{
    // Middleware to report the attendance of one service across to another.
    public class AttendanceRelayMiddleware :
        IMiddleware
    {
        private class RelayChannel
        {
            public IChatServiceConnection FromChatService;
            public IChatServiceConnection ToChatService;
            public string ToChannelName;
            public string ToChannelId;
            public string Prefix;
        }

        /// <summary>
        /// Logging instance
        /// </summary>
        private ILogger<AttendanceRelayMiddleware> _logger;

        /// <summary>
        /// Configuration object for retrieving configuration values
        /// </summary>
        private ConfigurationSection _configuration;

        /// <summary>
        /// List of chat service connections that we can use to announce streams.
        /// </summary>
        private IEnumerable<IChatServiceConnection> _chatServiceConnections;

        private List<RelayChannel> _relays = new List<RelayChannel>();

        public AttendanceRelayMiddleware(
            ILogger<AttendanceRelayMiddleware> logger,
            ConfigurationSection configuration,
            IEnumerable<IChatServiceConnection> chatServiceConnections)
        {
            _logger = logger;
            _configuration = configuration;
            _chatServiceConnections = chatServiceConnections;

            _logger.LogInformation("Starting AttendanceRelayMiddleware Middleware...");

            setupRelays();
        }

        private void setupRelays()
        {
            _logger.LogInformation("Setting up attendance relays...");
            var relayConfigs = _configuration.GetSection("Relays").GetChildren();
            foreach (var relayConfig in relayConfigs)
            {
                var fromChatServiceName = relayConfig.GetValue<string>("FromChatServiceName");
                var toChatServiceName = relayConfig.GetValue<string>("ToChatServiceName");
                var toChannelName = relayConfig.GetValue<string>("ToChannelName");
                var prefix = relayConfig.GetValue<string>("Prefix");
                var fromChatService = _chatServiceConnections
                    .SingleOrDefault(c => c.Name == fromChatServiceName);
                var toChatService = _chatServiceConnections
                    .SingleOrDefault(c => c.Name == toChatServiceName);
                if (fromChatService == null || toChatService == null)
                {
                    _logger.LogError(
                        "Could not find chat services. Check to make sure {from} and {to} exist.",
                        fromChatServiceName,
                        toChatServiceName);
                }
                else
                {
                    _relays.Add(new RelayChannel()
                    {
                        FromChatService = fromChatService,
                        ToChatService = toChatService,
                        ToChannelName = toChannelName,
                        Prefix = prefix
                    });
                    _logger.LogInformation("Added new relay: {from}->{to}/{channel}",
                        fromChatServiceName,
                        toChatServiceName,
                        toChannelName);
                }
            }
            // Subscribe to connected event
            var toChatServices = _relays.Select(r => r.ToChatService).Distinct();
            foreach (var chatService in toChatServices)
            {
                chatService.Connected += chatServiceConnected;
            }
        }

        private async void chatServiceConnected(object sender, EventArgs e)
        {
            IChatServiceConnection chatServiceConnection = sender as IChatServiceConnection;
            var relays = _relays.Where(r => r.ToChatService == chatServiceConnection);
            foreach (var relay in relays)
            {
                // Get channel ID
                var newChannelId = 
                    await relay.ToChatService
                        .GetChannelIdFromChannelNameAsync(relay.ToChannelName);
                if (relay.ToChannelId != newChannelId)
                {
                    Thread.Sleep(1000); // Quick and dirty way to avoid too many updates at once
                                        // TODO: Add a good debounce here
                    relay.ToChannelId = newChannelId;
                    // Send first update
                    onlineUsersChanged(relay.FromChatService);
                    // Subscribe to future updates
                    relay.FromChatService.OnlineUsers.CollectionChanged += 
                        (s, a) => onlineUsersChanged(relay.FromChatService);
                }
            }
        }

        private async void onlineUsersChanged(IChatServiceConnection chatServiceConnection)
        {
            var attendanceString = 
                String.Join(", ", chatServiceConnection.OnlineUsers.Select(u => u.Name));
            _logger.LogInformation("Users changed for {service}: {attendance}",
                chatServiceConnection.Name,
                attendanceString);
            var relays = _relays.Where(c => c.FromChatService == chatServiceConnection);
            foreach (var relay in relays)
            {
                if (relay.ToChannelId != null)
                {
                    await relay.ToChatService.SetChannelTopicAsync(
                        relay.ToChannelId,
                        relay.Prefix + attendanceString);
                    _logger.LogInformation("Set topic for {service}: {channel}:{id}",
                        relay.ToChatService.Name,
                        relay.ToChannelName,
                        relay.ToChannelId);
                }
                
            }
        }

        public MiddlewareResult ProcessMessage(ChatMessageModel message)
        {
            return MiddlewareResult.Continue;
        }
    }
}