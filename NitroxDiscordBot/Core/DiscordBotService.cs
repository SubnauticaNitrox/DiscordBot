using System.Collections.Concurrent;
using NitroxDiscordBot.Services;

namespace NitroxDiscordBot.Core;

/// <summary>
///     Default service implementation for Discord Bot services that waits for the bot to get ready when starting.
/// </summary>
public abstract class DiscordBotService : IHostedService, IDisposable
{
    private readonly Lazy<ConcurrentBag<IDisposable>> disposablesOnStop = new(LazyThreadSafetyMode.PublicationOnly);
    private readonly Lazy<ConcurrentBag<IDisposable>> disposablesOnDispose = new(LazyThreadSafetyMode.PublicationOnly);
    protected NitroxBotService Bot { get; }
    protected ILogger Log { get; }

    protected DiscordBotService(NitroxBotService bot, ILogger log)
    {
        ArgumentNullException.ThrowIfNull(bot);
        ArgumentNullException.ThrowIfNull(log);
        Bot = bot;
        Log = log;
    }

    async Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        await Bot.WaitForReadyAsync(cancellationToken);
        if (!Bot.IsConnected)
        {
            throw new Exception("Discord bot has not started yet");
        }

        await StartAsync(cancellationToken);
        Log.LogInformation("Service started");
    }

    async Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        await StopAsync(cancellationToken);
        if (disposablesOnStop.IsValueCreated)
        {
            foreach (IDisposable disposable in disposablesOnStop.Value)
            {
                disposable.Dispose();
            }
            disposablesOnStop.Value.Clear();
        }
        Log.LogInformation("Service stopped");
    }

    /// <summary>
    ///     Disposes the given disposable when the service is disposed.
    /// </summary>
    protected void RegisterDisposable(IDisposable disposable, bool disposeOnServiceStop = false)
    {
        if (disposeOnServiceStop)
        {
            disposablesOnStop.Value.Add(disposable);
        }
        else
        {
            disposablesOnDispose.Value.Add(disposable);
        }
    }

    public abstract Task StartAsync(CancellationToken cancellationToken);

    public virtual Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public virtual void Dispose()
    {
        if (disposablesOnDispose.IsValueCreated)
        {
            foreach (IDisposable disposable in disposablesOnDispose.Value)
            {
                disposable.Dispose();
            }
            disposablesOnDispose.Value.Clear();
        }
    }
}
