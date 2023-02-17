﻿using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using NitroxDiscordBot.Configuration;

namespace NitroxDiscordBot.Services;

/// <summary>
///     Connects to the Discord API as a bot and provides an abstraction over the Discord API for other services.
/// </summary>
public class NitroxBotService : IHostedService, IDisposable
{
    private readonly ILogger log;
    private readonly DiscordSocketClient client;
    private readonly IOptionsMonitor<NitroxBotConfig> config;

    /// <summary>
    ///     Used as anchor point for fetching early messages from a Discord channel.
    /// </summary>
    private const ulong EarliestSnowflakeId = 5000000;

    public NitroxBotService(IOptionsMonitor<NitroxBotConfig> config, ILogger<NitroxBotService> log)
    {
        this.config = config;
        this.log = log;
        client = new DiscordSocketClient();
        client.Log += ClientLogReceived;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await client.LoginAsync(TokenType.Bot, config.CurrentValue.Token);
        await client.StartAsync();
        await WaitForReadyAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await client.StopAsync();
    }

    public async Task DeleteOldMessagesAsync(ulong channelId, TimeSpan age, CancellationToken cancellationToken)
    {
        IMessageChannel channel = await GetChannel<IMessageChannel>(channelId);
        if (channel == null)
        {
            return;
        }

        var count = 0;
        log.LogInformation("Running old messages cleanup on channel '{ChannelName}'", channel.Name);
        await foreach (IReadOnlyCollection<IMessage> buffer in channel.GetMessagesAsync(EarliestSnowflakeId, Direction.After).WithCancellation(cancellationToken))
        {
            if (buffer == null || buffer.Count < 1)
            {
                continue;
            }
            // Messages from API seem to be in reverse chronological order. But in case the API changes, let's order it ourselves again.
            IMessage[] chronologicalMessages = buffer.Reverse().OrderBy(m => m.Timestamp).ToArray();

            if (channel is ITextChannel textChannel)
            {
                // Channel supports bulk delete, do this instead.
                IMessage[] messagesToBulkDelete = chronologicalMessages.TakeWhile(m => m.Timestamp + age < DateTimeOffset.UtcNow).ToArray();
                if (messagesToBulkDelete.Length > 0)
                {
                    var messagesSummary = string.Join(Environment.NewLine, messagesToBulkDelete.Select(m => $@"{m.Timestamp}:{Environment.NewLine}{m.Content.Replace("\n", "\t" + Environment.NewLine)}"));
                    log.LogInformation("Deleting messages:{NewLine}{MessagesContent}", Environment.NewLine, messagesSummary);
                    await textChannel.DeleteMessagesAsync(messagesToBulkDelete);
                    count += messagesToBulkDelete.Length;

                    // Exit early instead of iterating all messages in channel.
                    bool bufferContainedMessageThatDoesNotNeedDeletion = messagesToBulkDelete.Length != chronologicalMessages.Length;
                    if (bufferContainedMessageThatDoesNotNeedDeletion)
                    {
                        goto chronologicallyDone;
                    }
                }
            }
            else
            {
                // Channel does not support bulk delete, remove one-by-one. Slow: Discord rate limits are easy to hit.
                foreach (IMessage message in chronologicalMessages)
                {
                    if (message.Timestamp + age < DateTimeOffset.UtcNow)
                    {
                        log.LogInformation("Deleting message: '{MessageContent}' with timestamp: {MessageTimestamp}", message.Content, message.Timestamp);
                        await message.DeleteAsync();
                        count++;
                    }
                    else
                    {
                        // Exit early instead of iterating all messages in channel.
                        goto chronologicallyDone;
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
        }
        chronologicallyDone:

        if (count > 0)
        {
            log.LogInformation("Deleted {Count} message(s) older than {Age} from channel '{ChannelName}'", count, age, channel.Name);
        }
        else
        {
            log.LogInformation("Nothing needed to be deleted from channel '{ChannelName}'", channel.Name);
        }
    }

    public async Task CreateOrUpdateMessage(ulong channelId, int index, Embed embed)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        IMessageChannel channel = await GetChannel<IMessageChannel>(channelId);
        if (channel == null)
        {
            return;
        }

        // Send message if index is outside of total messages.
        IMessage[] messages = await GetMessagesAsync(channel, index + 2);
        if (index >= messages.Length)
        {
            await channel.SendMessageAsync(null, false, embed);
            return;
        }
        // If author of current message is different then we can't edit it.
        if (messages[index].Author.Id != client.CurrentUser?.Id)
        {
            log.LogError("Unable to modify message at index {Index} because it is authored by another user: '{AuthorUsername}' ({AuthorId})", index,
                messages[index].Author.Username, messages[index].Author.Id);
            return;
        }

        // Modify message that is authored by this bot.
        await channel.ModifyMessageAsync(messages[index].Id, props =>
        {
            props.Content = "";
            props.Embed = embed;
        });
    }

    private async Task<IMessage[]> GetMessagesAsync(IMessageChannel channel, int limit = 100, bool sorted = true)
    {
        IEnumerable<IMessage> messages = await channel.GetMessagesAsync(EarliestSnowflakeId, Direction.After, limit).FlattenAsync();
        if (sorted)
        {
            messages = messages.OrderBy(m => m.Timestamp);
        }
        return messages.ToArray();
    }

    private async Task<T> GetChannel<T>(ulong channelId) where T : class, IChannel
    {
        T channel = await client.GetChannelAsync(channelId) as T;
        if (channel == null)
        {
            log.LogWarning("Couldn't find channel of type {ChannelType} with id {ChannelId}", typeof(T).Name, channelId);
        }
        return channel;
    }

    public async Task WaitForReadyAsync(CancellationToken cancellationToken)
    {
        await Task.Run(async () =>
        {
            var enteredLoop = false;
            while (!cancellationToken.IsCancellationRequested && !IsConnected)
            {
                enteredLoop = true;
                await Task.Delay(100, cancellationToken);
            }

            // Wait some extra time before trying to use API
            if (enteredLoop && IsConnected)
            {
                await Task.Delay(1000, cancellationToken);
            }
        }, cancellationToken);
    }

    public bool IsConnected => client.ConnectionState == ConnectionState.Connected;

    /// <summary>
    ///     Handler that receives log messages from the Discord client API.
    /// </summary>
    private Task ClientLogReceived(LogMessage logEntry)
    {
        if (logEntry.Exception is not null)
        {
            log.LogError(logEntry.Exception, "Discord API error");
        }
        else
        {
            log.LogInformation(logEntry.Message);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        client.Log -= ClientLogReceived;
        client.Dispose();
    }
}
