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
        "GlimeshStreamAnnouncement": {
          "Enabled": true,
          "ClientId": "CLIENTIDHERE",
          "ClientSecret": "CLIENTSECRETHERE",
          "GlimeshUsernames": [
            "chickencam",
            "HammyCheesy",
            "SmashBets"
          ],
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
        }
    }
}
```