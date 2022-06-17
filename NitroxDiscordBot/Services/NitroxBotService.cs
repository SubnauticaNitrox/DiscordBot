using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    ///     Used as anchor point for fetching early messages.
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
        IMessageChannel? channel = await GetChannel<IMessageChannel>(channelId);
        if (channel == null)
        {
            return;
        }

        var count = 0;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        log.LogInformation($"Running old messages cleanup on channel '{channel.Name}'");
        await foreach (IReadOnlyCollection<IMessage>? buffer in channel.GetMessagesAsync(EarliestSnowflakeId, Direction.After).WithCancellation(cancellationToken))
        {
            if (buffer == null)
            {
                continue;
            }

            foreach (IMessage message in buffer)
            {
                if (message.Timestamp + age < now)
                {
                    log.LogInformation($"Deleting message: '{message.Content}' with timestamp: {message.Timestamp}");
                    await message.DeleteAsync();
                    count++;
                }
            }
        }

        if (count > 0)
        {
            log.LogInformation($"Deleted {count} message(s) older than {age} from channel '{channel.Name}'");
        }
        else
        {
            log.LogInformation($"Nothing was deleted from channel '{channel.Name}'");
        }
    }

    public async Task CreateOrUpdateMessage(ulong channelId, int index, Embed embed)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        IMessageChannel? channel = await GetChannel<IMessageChannel>(channelId);
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
            log.LogError($"Unable to modify message at index {index} because it is authored by another user: '{messages[index].Author.Username}' ({messages[index].Author.Id})");
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

    private async Task<T?> GetChannel<T>(ulong channelId) where T : class, IChannel
    {
        T? channel = await client.GetChannelAsync(channelId) as T;
        if (channel == null)
        {
            log.LogWarning($"Couldn't find channel of type {typeof(T).Name} with id {channelId}");
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
            log.LogError(logEntry.Exception.ToString());
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