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
            "Middleware": [ "Echo", "MixerStreamAnnouncement" ]
        },
        "WarmItUpSlack": {
            "Service": "Slack",
            "ApiToken": "yourapitokenhere",
            "Middleware": [ "Echo" ]
        }
    },
    "Middleware": {
        "Echo": {
            "Enabled": true
        },
        "MixerStreamAnnouncement": {
            "Enabled": true,
            "ClientId": "CLIENTIDHERE",
            "MixerChannels": [ "HammyCheesy" ],
            "AnnounceChannels": [
                {
                    "ChatServiceName": "WarmItUpMattermost",
                    "ChannelName": "gaming"
                }
            ]
        }
    }
}
```