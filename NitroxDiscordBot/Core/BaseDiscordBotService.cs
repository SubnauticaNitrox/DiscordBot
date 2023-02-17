using NitroxDiscordBot.Services;

namespace NitroxDiscordBot.Core;

/// <summary>
///     Default service implementation for Discord Bot services that waits for the bot to get ready when starting.
/// </summary>
public abstract class BaseDiscordBotService : IHostedService
{
    protected NitroxBotService Bot { get; }

    protected BaseDiscordBotService(NitroxBotService bot)
    {
        Bot = bot;
    }

    async Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        await Bot.WaitForReadyAsync(cancellationToken);
        if (!Bot.IsConnected)
        {
            throw new Exception("Discord bot has not started yet");
        }

        await StartAsync(cancellationToken);
    }

    public abstract Task StartAsync(CancellationToken cancellationToken);

    public abstract Task StopAsync(CancellationToken cancellationToken);
}
