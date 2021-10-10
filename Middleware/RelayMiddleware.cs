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
    // Middleware to relay messages and attendance of one service across to another.
    public class RelayMiddleware :
        IMiddleware
    {
        private class RelayChannel
        {
            public bool IsActive;
            public IChatServiceConnection FromChatService;
            public string FromChannelName;
            public string FromChannelId;
            public IChatServiceConnection ToChatService;
            public string ToChannelName;
            public string ToChannelId;
            public string AttendancePrefix;
            public string ChatPrefix;
            public bool RelayChat;
            public bool RelayAttendance;
        }

        /// <summary>
        /// Logging instance
        /// </summary>
        private ILogger<RelayMiddleware> _logger;

        /// <summary>
        /// Configuration object for retrieving configuration values
        /// </summary>
        private ConfigurationSection _configuration;

        /// <summary>
        /// List of chat service connections that we can use to announce streams.
        /// </summary>
        private IEnumerable<IChatServiceConnection> _chatServiceConnections;

        private List<RelayChannel> _relays = new List<RelayChannel>();

        public RelayMiddleware(
            ILogger<RelayMiddleware> logger,
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
                var fromChatServiceName  = relayConfig.GetValue<string>("FromChatServiceName");
                var fromChannelName      = relayConfig.GetValue<string>("FromChannelName");
                var toChatServiceName    = relayConfig.GetValue<string>("ToChatServiceName");
                var toChannelName        = relayConfig.GetValue<string>("ToChannelName");
                var attendancePrefix     = relayConfig.GetValue<string>("AttendancePrefix");
                var chatPrefix           = relayConfig.GetValue<string>("ChatPrefix");
                var relayChat            = relayConfig.GetValue<bool>  ("RelayChat");
                var relayAttendance      = relayConfig.GetValue<bool>  ("RelayAttendance");

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
                        IsActive         = false,
                        FromChatService  = fromChatService,
                        FromChannelName  = fromChannelName,
                        ToChatService    = toChatService,
                        ToChannelName    = toChannelName,
                        AttendancePrefix = attendancePrefix,
                        ChatPrefix       = chatPrefix,
                        RelayChat        = relayChat,
                        RelayAttendance  = relayAttendance,
                    });
                    _logger.LogInformation(
                        "Added new [chat: {chat}, attendance: {attendance}] relay: {fromService}/{fromChannel} -> {toService}/{toChannel}",
                        relayChat,
                        relayAttendance,
                        fromChatServiceName,
                        fromChannelName,
                        toChatServiceName,
                        toChannelName);
                }
            }

            // Subscribe to connected event
            var fromChatServices = _relays.Select(r => r.FromChatService).Distinct();
            var toChatServices = _relays.Select(r => r.ToChatService).Distinct();
            var allChatServices = fromChatServices.Concat(toChatServices).Distinct();

            // Wait for all services to connect so we can extract channel and user info
            foreach (var chatService in allChatServices)
            {
                chatService.Connected += chatServiceConnected;
            }
        }

        private async void chatServiceConnected(object sender, EventArgs e)
        {
            IChatServiceConnection chatServiceConnection = sender as IChatServiceConnection;
            var relays = _relays.Where(r => 
                (r.ToChatService == chatServiceConnection) ||
                (r.FromChatService == chatServiceConnection))
                .Distinct();
            foreach (var relay in relays)
            {
                // Update channel IDs
                if (relay.FromChatService == chatServiceConnection)
                {
                    relay.FromChannelId = await chatServiceConnection
                        .GetChannelIdFromChannelNameAsync(relay.FromChannelName);
                }
                if (relay.ToChatService == chatServiceConnection)
                {
                    relay.ToChannelId = await chatServiceConnection
                        .GetChannelIdFromChannelNameAsync(relay.ToChannelName);
                }

                // Are both services ready to roll?
                if (!(relay.IsActive) && 
                    (relay.FromChatService.IsConnected && relay.ToChatService.IsConnected))
                {
                    if (relay.RelayAttendance)
                    {
                        // Send first update
                        onlineUsersChanged(relay.FromChatService);
                        // Subscribe to future updates
                        relay.FromChatService.OnlineUsers.CollectionChanged += 
                            (s, a) => onlineUsersChanged(relay.FromChatService);
                    }

                    relay.IsActive = true;
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
            var relays = _relays.Where(c => 
                c.RelayAttendance && 
                (c.FromChatService == chatServiceConnection));
            foreach (var relay in relays)
            {
                if (relay.ToChannelId != null)
                {
                    await relay.ToChatService.SendMessageToChannelIdAsync(
                        relay.ToChannelId,
                        new ChatMessageModel()
                        {
                            Body = $"{relay.AttendancePrefix}{attendanceString}",
                            Attachments = null,
                        });
                    _logger.LogInformation("Set topic for {service}: {channel}:{id}",
                        relay.ToChatService.Name,
                        relay.ToChannelName,
                        relay.ToChannelId);
                }
            }
        }

        public MiddlewareResult ProcessMessage(ChatMessageModel message)
        {
            if (!(message.IsFromTheorem))
            {
                // Is this from a service/channel that we relay messages from?
                var matchingRelays = _relays.Where(r => 
                    r.RelayChat && 
                    (r.FromChatService == message.FromChatServiceConnection) &&
                    (r.FromChannelId == message.ChannelId));
                foreach (var relay in matchingRelays)
                {
                    // replay this message to the destination
                    if (relay.IsActive)
                    {
                        string displayName = message.AuthorId;
                        var messageUser = message.
                            FromChatServiceConnection.
                            Users.
                            SingleOrDefault(u => u.Id == message.AuthorId);
                        if (messageUser != null)
                        {
                            displayName = messageUser.Name;
                        }
                        relay.ToChatService.SendMessageToChannelIdAsync(
                            relay.ToChannelId,
                            new ChatMessageModel()
                            {
                                Body = $"{relay.ChatPrefix}{displayName}: {message.Body}",
                                Attachments = message.Attachments?.ToList(),
                            });
                    }
                }
            }
            return MiddlewareResult.Continue;
        }
    }
}