using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Theorem.ChatServices;
using Theorem.Models;

namespace Theorem.Middleware
{
    // Middleware to relay messages and attendance of one service across to another.
    public class RelayMiddleware : IMiddleware
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
            public string ChatPrefix;
            public string AttendanceStarted;
            public string AttendancePrefix;
            public string AttendanceEndedPrefix;
            public bool RelayChat;
            public bool RelayAttendance;

            public DateTimeOffset AttendanceSessionStarted;
            public string AttendanceSessionMessageId;
            public IList<UserModel> CurrentAttendanceSessionUsers;
            public IList<UserModel> TotalAttendanceSessionUsers;
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
                var fromChatServiceName   = relayConfig.GetValue<string>("FromChatServiceName");
                var fromChannelName       = relayConfig.GetValue<string>("FromChannelName");
                var toChatServiceName     = relayConfig.GetValue<string>("ToChatServiceName");
                var toChannelName         = relayConfig.GetValue<string>("ToChannelName");
                var chatPrefix            = relayConfig.GetValue<string>("ChatPrefix");
                var attendanceStarted     = relayConfig.GetValue<string>("AttendanceStarted");
                var attendancePrefix      = relayConfig.GetValue<string>("AttendancePrefix");
                var attendanceEndedPrefix = relayConfig.GetValue<string>("AttendanceEndedPrefix");
                var relayChat             = relayConfig.GetValue<bool>  ("RelayChat");
                var relayAttendance       = relayConfig.GetValue<bool>  ("RelayAttendance");

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
                        IsActive                      = false,
                        FromChatService               = fromChatService,
                        FromChannelName               = fromChannelName,
                        ToChatService                 = toChatService,
                        ToChannelName                 = toChannelName,
                        ChatPrefix                    = chatPrefix,
                        AttendanceStarted             = attendanceStarted,
                        AttendancePrefix              = attendancePrefix,
                        AttendanceEndedPrefix         = attendanceEndedPrefix,
                        RelayChat                     = relayChat,
                        RelayAttendance               = relayAttendance,
                        AttendanceSessionStarted      = DateTimeOffset.MinValue,
                        AttendanceSessionMessageId    = "",
                        CurrentAttendanceSessionUsers = new List<UserModel>(),
                        TotalAttendanceSessionUsers   = new List<UserModel>(),
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
                        channelsUpdated(relay.FromChatService, relay.FromChatService.Channels);
                        // Subscribe to future updates
                        relay.FromChatService.ChannelsUpdated += 
                            (_, channels) => channelsUpdated(relay.FromChatService, channels);
                    }

                    relay.IsActive = true;
                }
            }
        }

        private async void channelsUpdated(IChatServiceConnection chatServiceConnection,
            ICollection<ChannelModel> channels)
        {
            // TODO: mutex
            var relays = _relays.Where(c => 
                c.RelayAttendance && 
                (c.FromChatService == chatServiceConnection));
            foreach (var relay in relays)
            {
                if (relay.ToChannelId != null)
                {
                    var channelUpdate = 
                        channels.SingleOrDefault(c => (c.Id == relay.FromChannelId));
                    if (channelUpdate != null)
                    {
                        await processAttendanceUpdateAsync(relay, channelUpdate);
                    }
                }
            }
        }

        private async Task processAttendanceUpdateAsync(RelayChannel relay,
            ChannelModel channelUpdate)
        {
            var updatedAttendance = channelUpdate.Users
                .Where(u => (!u.IsTheorem) && (u.Presence == UserModel.PresenceKind.Online))
                .ToList();
            if (isUserListEqual(relay.CurrentAttendanceSessionUsers, updatedAttendance))
            {
                return;
            }
            if (relay.CurrentAttendanceSessionUsers.Count > 0)
            {
                await updateAttendanceSessionAsync(relay, updatedAttendance);
            }
            else
            {
                await startNewAttendanceSessionAsync(relay, updatedAttendance);
            }
        }

        private async Task startNewAttendanceSessionAsync(RelayChannel relay, List<UserModel> users)
        {
            relay.TotalAttendanceSessionUsers.Clear();
            relay.AttendanceSessionStarted = DateTimeOffset.Now;
            updateRelayAttendance(relay, users);
            var attendanceString = string.Join(", ", users.Select(u => u.DisplayName));
            var messageBody = $"{relay.AttendancePrefix} {attendanceString}";
            var messageModel = new ChatMessageModel()
            {
                Body = messageBody
            };
            var messageId = await relay.ToChatService
                .SendMessageToChannelIdAsync(relay.ToChannelId, messageModel);
            relay.AttendanceSessionMessageId = messageId;
        }

        private async Task updateAttendanceSessionAsync(RelayChannel relay, List<UserModel> users)
        {
            updateRelayAttendance(relay, users);
            if (users.Count == 0)
            {
                await endAttendanceSessionAsync(relay);
                return;
            }

            var attendanceString = string.Join(", ", users.Select(u => u.DisplayName));
            var messageBody = $"{relay.AttendancePrefix} {attendanceString}";
            var messageModel = new ChatMessageModel()
            {
                Body = messageBody
            };
            relay.AttendanceSessionMessageId = await relay.ToChatService
                .UpdateMessageAsync(relay.ToChannelId, relay.AttendanceSessionMessageId,
                    messageModel);
        }

        private async Task endAttendanceSessionAsync(RelayChannel relay)
        {
            // Edit the original message to mark when the call started,
            var startedMessageModel = new ChatMessageModel()
            {
                Body = relay.AttendanceStarted
            };
            await relay.ToChatService
                .UpdateMessageAsync(relay.ToChannelId, relay.AttendanceSessionMessageId,
                    startedMessageModel);

            // ... and post a new message to show when the call ended.
            var duration = DateTimeOffset.Now - relay.AttendanceSessionStarted;
            var durationStr = duration.ToString("h'h 'm'm 's's'");
            var totalAttendanceStr = string.Join(", ",
                relay.TotalAttendanceSessionUsers.Select(u => u.DisplayName));
            var messageBody = $"{relay.AttendanceEndedPrefix} Duration: {durationStr}, " + 
                $"Participants: {totalAttendanceStr}";
            var messageModel = new ChatMessageModel()
            {
                Body = messageBody
            };
            await relay.ToChatService
                .SendMessageToChannelIdAsync(relay.ToChannelId, messageModel);

            relay.AttendanceSessionStarted = DateTimeOffset.MinValue;
            relay.AttendanceSessionMessageId = "";
            relay.CurrentAttendanceSessionUsers.Clear();
            relay.TotalAttendanceSessionUsers.Clear();
        }

        private void updateRelayAttendance(RelayChannel relay, List<UserModel> users)
        {
            foreach (var user in users)
            {
                if (!(relay.TotalAttendanceSessionUsers.Any(u => (u.Id == user.Id))))
                {
                    relay.TotalAttendanceSessionUsers.Add(user);
                }
            }
            relay.CurrentAttendanceSessionUsers = users;
        }

        private bool isUserListEqual(IList<UserModel> first, IList<UserModel> second)
        {
            if (first.Count != second.Count)
            {
                return false;
            }
            var firstIds = first.Select(u => u.Id).ToHashSet();
            var secondIds = second.Select(u => u.Id).ToHashSet();
            foreach(var id in firstIds)
            {
                if (!secondIds.Contains(id))
                {
                    return false;
                }
            }
            foreach(var id in secondIds)
            {
                if (!firstIds.Contains(id))
                {
                    return false;
                }
            }
            return true;
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
                        if (message.AuthorAlias.Length > 0)
                        {
                            displayName = message.AuthorAlias;
                        }
                        if (message.AuthorDisplayName.Length > 0)
                        {
                            displayName = message.AuthorDisplayName;
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