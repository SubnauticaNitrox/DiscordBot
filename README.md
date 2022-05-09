# NitroxDiscordBot
Discord bot used by Nitrox team.

## Setup

1. Ask for the token (as opposed to reset)
2. Create "appsettings.json" file and set the token value.
3. Add the bot to your private server by following the steps (bot invite URL is generated [here](https://discord.com/developers/applications/405122994348752896/oauth2/url-generator)):  
https://discord.com/api/oauth2/authorize?client_id=405122994348752896&permissions=17179943952&scope=bot
4. Run this application

## Example appsettings.json file
```json
{
    "Token": "DISCORD_TOKEN_HERE",
    "CleanupTasks": [
        {
            "ChannelId": 598546552918900774,
            "MaxAge": "0:1",
            "Schedule": "* * * * *"
        }
    ]
}
```
