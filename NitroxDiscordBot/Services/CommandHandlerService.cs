﻿using Discord;
using Discord.WebSocket;
using NitroxDiscordBot.Core;

namespace NitroxDiscordBot.Services;

/// <summary>
///     Legacy command handler service just for the ?ping command.
/// </summary>
public class CommandHandlerService : DiscordBotHostedService
{
    private const char CommandPrefix = '?';
    private const string PingCommandName = "ping";

    public CommandHandlerService(NitroxBotService bot,
        ILogger<CommandHandlerService> log) : base(bot, log)
    {
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        Bot.MessageReceived += BotOnMessageReceived;
        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        Bot.MessageReceived -= BotOnMessageReceived;
        return Task.CompletedTask;
    }

    private void BotOnMessageReceived(object sender, SocketMessage message)
    {
        if (message is not { Author: SocketGuildUser author } || author.IsBot)
        {
            return;
        }
        // Only allow some kind of moderator to use commands.
        if (!author.GuildPermissions.ManageMessages)
        {
            return;
        }

        // Parse the command name and lower-case it.
        ReadOnlySpan<char> messyCommandName = GetCommandNamePartFromMessageContent(message.Content);
        Span<char> commandName = messyCommandName.Length <= byte.MaxValue ? stackalloc char[messyCommandName.Length] : new char[messyCommandName.Length];
        messyCommandName.ToLowerInvariant(commandName);

        // Lookup command action for the command name.
        switch (commandName)
        {
            case PingCommandName:
                _ = PingAsync(message).ContinueWith(t =>
                {
                    if (t is { IsFaulted: true, Exception: Exception ex })
                    {
                        Log.CommandError(ex, message.Content, message.Author.Id, message.Author.Username);
                    }
                });
                break;
        }
    }

    private async Task PingAsync(IMessage command)
    {
        try
        {
            IUserMessage pongMessage = await command.Channel.SendMessageAsync("Pong!", allowedMentions: AllowedMentions.None);
            TimeSpan timeDiff = pongMessage.Timestamp - command.Timestamp;
            await pongMessage.ModifyAsync(m => m.Content = $"Pong! `{timeDiff.TotalMilliseconds}ms`");
        }
        catch (Exception ex)
        {
            Log.CommandError(ex, command.CleanContent, command.Author.Id, command.Author.Username);
        }
    }

    private ReadOnlySpan<char> GetCommandNamePartFromMessageContent(ReadOnlySpan<char> content)
    {
        if (content is { Length: <= 1 } || content[0] != CommandPrefix || content[1] == ' ')
        {
            return [];
        }
        ReadOnlySpan<char> messyCommandName = content.Slice(1).Trim();
        if (messyCommandName.IsEmpty)
        {
            return [];
        }
        if (messyCommandName.IndexOf(' ') is var spaceIndex and >= 0)
        {
            messyCommandName = messyCommandName.Slice(0, spaceIndex);
        }
        return messyCommandName;
    }
}