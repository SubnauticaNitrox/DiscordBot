# NitroxDiscordBot
Discord bot used by Nitrox team.

## Setup

1. Ask for the token (as opposed to reset)
2. Create "appsettings.Development.json" file and set the token value.
3. Add the bot to your private server by following the steps (bot invite URL is generated [here](https://discord.com/developers/applications/405122994348752896/oauth2/url-generator)):  
https://discord.com/api/oauth2/authorize?client_id=405122994348752896&permissions=17179943952&scope=bot
4. Execute `dev-run.sh` script.

## Deploy

 - With the Rider IDE you can connect to a docker host via SSH to push & build the container remotely.
 - To build it from CLI, see below.

1. Build the docker container:
    ```shell
    docker build --tag nitroxdiscordbot:latest -f ./NitroxDiscordBot/Dockerfile .
    ```
2. Run the docker container:
    ```shell
    docker run --name nitroxdiscordbot nitroxdiscordbot:latest
    ```

## Features
 - Purging channels of "old" messages

## Example appsettings.json file
```json
{
    "Token": "DISCORD_TOKEN_HERE",
    "CleanupDefinitions": [
        {
            "ChannelId": 598546552918900774,
            "MaxAge": "1.0:0",
            "Schedule": "* * * * *"
        }
    ]
}
```
MaxAge is a `TimeSpan` format for 1 day