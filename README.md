# Theorem Chat Bot
Theorem is a generic chat bot I built for whatever the eff I want. 💅

More docs later. I'm just using this to store some useful info.

# appsettings.json example
```json
{
    "ChatServiceConnections": {
        "WarmItUpMattermost": {
            "Service": "Mattermost",
            "ServerHostname": "chat.myserver.com",
            "AccessToken": "youraccesstokenhere",
            "Middleware": [ "Echo", "EzFtlStreamAnnouncement" ]
        },
        "WarmItUpSlack": {
            "Service": "Slack",
            "ApiToken": "yourapitokenhere",
            "Middleware": [ "Echo" ]
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
            "Hostname": "myezftlinstance.tv",
            "Port": 80,
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
        }
    }
}
```