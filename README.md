# NitroxDiscordBot
Discord bot used by Nitrox team.

## Setup

1. Ask for the Discord bot token (as opposed to reset)
2. Create "appsettings.Development.json" file and set the `Token` + `GuildId`. See below for appsettings.json template.
3. Add the bot to your private server by following the steps (bot invite URL is generated [here](https://discord.com/developers/applications/405122994348752896/oauth2/url-generator)):
https://discord.com/api/oauth2/authorize?client_id=405122994348752896&permissions=17179943952&scope=bot
4. Deploy the bot or run it locally in development mode for testing.

## Deploy (docker)

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
 - Purging channels of "old" messages (configure with `/cleanup`)
 - Auto respond to user messages (configure with `/autoresponse`)
### Backend features
 - Sqlite database - which is stored in `/app/data/nitroxbot.db`. Docker mount the `/app/data` directory somewhere on your host to reuse the database. Otherwise, the database is gone when the docker container is deleted and you need to reconfigure the bot again.

## Example appsettings.json file
```json
{
    "Token": "DISCORD_TOKEN_HERE",
    "GuildId": MY_SERVER_ID_HERE,
    "Developers": [MY_DISCORD_USER_ID]
}
```
 - GuildId is needed as this bot is meant to manage only 1 server at a time.
 - Developers field is optional but gives you super admin access to the commands of the bot, no matter the Discord server.