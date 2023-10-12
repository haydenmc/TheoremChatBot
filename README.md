# Theorem Chat Bot

Theorem is a generic chat bot I built for whatever the eff I want. 💅

More docs later. I'm just using this to store some useful info.

# appsettings.json example

```json
{
    "ChatServiceConnections": {
        "WarmItUpMatrix": {
            "Service": "Matrix",
            "BaseUrl": "https://your.service",
            "UserName": "theorem",
            "Password": "PASSWORD",
            "RoomServerRestriction": "your.service",
            "Middleware": [ "Echo", "EzFtlStreamAnnouncement" ]
        },
        "WarmItUpMumble": {
            "Service": "Mumble",
            "ServerHostname": "",
            "ServerPort": 64738,
            "Username": "Theorem",
            "ServerPassword": "",
            "Middleware": [ ]
        }
    },
    "Middleware": {
        "Echo": {
            "Enabled": true
        },
        "EzFtlStreamAnnouncement": {
            "Enabled": true,
            "BaseUrl": "https://myezftlinstance.tv",
            "WebSocketUrl": "wss://myezftlinstance.tv/ws",
            "AnnounceChannels": [
                {
                    "ChatServiceName": "WarmItUpMattermost",
                    "ChannelName": "gaming"
                },
                {
                    "ChatServiceName": "WarmItUpMumble",
                    "ChannelName": "Game Night"
                }
            ]
        },
        "AttendanceRelay": {
            "Enabled": true,
            "Relays": [
                {
                    "FromChatServiceName": "WarmItUpMumble",
                    "ToChatServiceName": "WarmItUpMattermost",
                    "ToChannelName": "gaming",
                    "Prefix": "Mumble: "
                }
            ]
        },
        "Volunteering": {
            "Enabled": true,
            "PostChannels": [
                {
                    "ChatServiceName": "WarmItUpMatrix",
                    "ChannelName": "#volunteering:warmitup.chat"
                }
            ],
            "PostDayOfWeek": "Monday",
            "PostTime": "08:00"
        },
        "Eastra": {
            "Enabled": true,
            "PostChannels": [
                {
                    "ChatServiceName": "WarmItUpMatrix",
                    "ChannelName": "#bot:warmitup.chat"
                }
            ],
            "WeeklyPostSchedules": [
                {
                    "RecurrenceStartDate": "2023-10-15",
                    "RecurrenceWeeklyInterval": 2,
                    "VoteDayOfWeek": "Sunday",
                    "VoteTime": "17:00",
                    "TimeToAnnounce": "39:00:00"
                },
                {
                    "RecurrenceStartDate": "2023-10-22",
                    "RecurrenceWeeklyInterval": 2,
                    "VoteDayOfWeek": "Sunday",
                    "VoteTime": "17:00",
                    "TimeToAnnounce": "63:00:00"
                }
            ]
        }
    }
}
```