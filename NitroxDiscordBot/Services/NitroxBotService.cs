using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NitroxDiscordBot.Configuration;

namespace NitroxDiscordBot.Services;

/// <summary>
///     Runs scheduled tasks for Nitrox Discord server.
/// </summary>
public class NitroxBotService : IHostedService, IDisposable
{
    private readonly ILogger log;
    private readonly DiscordSocketClient client;
    private readonly IOptionsMonitor<NitroxBotConfig> config;

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
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await client.StopAsync();
    }

    public async Task DeleteOldMessagesAsync(ulong channelId, TimeSpan age, CancellationToken cancellationToken)
    {
        IMessageChannel? channel = await client.GetChannelAsync(channelId) as IMessageChannel;
        if (channel == null)
        {
            log.LogWarning($"Couldn't find channel with id {channelId}");
            return;
        }

        var count = 0;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        log.LogInformation($"Running old messages cleanup on channel '{channel.Name}'");
        await foreach (IReadOnlyCollection<IMessage>? buffer in channel.GetMessagesAsync(400).WithCancellation(cancellationToken))
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