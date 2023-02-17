using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using NCrontab;
using NitroxDiscordBot.Configuration;
using NitroxDiscordBot.Core;

namespace NitroxDiscordBot.Services;

/// <summary>
///     Cleans up "old" messages from Discord channels.
/// </summary>
public class ChannelCleanupService : BaseDiscordBotService, IDisposable
{
    private readonly ILogger<ChannelCleanupService> log;

    /// <summary>
    ///     Gets a list of cleanup tasks mapped to a cleanup definition in the config file. Each config entry should have only 1 (future) cleanup task.
    /// </summary>
    private readonly ConcurrentDictionary<ChannelCleanupConfig.ChannelCleanup, DateTime> scheduledTasks = new();

    /// <summary>
    ///     Used to neatly exit this service.
    /// </summary>
    private CancellationTokenSource? serviceCancellationSource;

    private readonly IOptionsMonitor<ChannelCleanupConfig> options;
    private readonly IDisposable? configChangeSubscription;

    /// <summary>
    ///     Cleanup tasks that are submitted to, based on the <see cref="scheduledTasks" />.
    /// </summary>
    private readonly Channel<ChannelCleanupConfig.ChannelCleanup> workQueue = Channel.CreateBounded<ChannelCleanupConfig.ChannelCleanup>(20);

    public ChannelCleanupService(NitroxBotService bot, IOptionsMonitor<ChannelCleanupConfig> options, ILogger<ChannelCleanupService> log) : base(bot)
    {
        this.log = log;
        this.options = options;

        OptionsChanged(options.CurrentValue);
        configChangeSubscription = options.CreateObservable().Throttle(TimeSpan.FromSeconds(2)).Subscribe(OptionsChanged);
    }

    private void OptionsChanged(ChannelCleanupConfig obj)
    {
        scheduledTasks.Clear();

        var definitions = CleanupDefinitions.Count();
        if (definitions < 1)
        {
            log.LogInformation("Cleanup service disabled");
        }
        else
        {
            var cleanupTaskSummary = string.Join(Environment.NewLine, CleanupDefinitions.Select(t => t.ToString()));
            log.LogInformation("Found {Count} cleanup tasks:{NewLine}{CleanupTasks}", definitions, Environment.NewLine, cleanupTaskSummary);
        }
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        serviceCancellationSource = new CancellationTokenSource();

        _ = Task.Run(async () => await RunCleanupProducer(serviceCancellationSource.Token), cancellationToken);
        _ = Task.Run(async () => await RunCleanupConsumer(serviceCancellationSource.Token), cancellationToken);

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Processes the <see cref="workQueue">queued</see> cleanup work.
    /// </summary>
    private async Task RunCleanupConsumer(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await foreach (ChannelCleanupConfig.ChannelCleanup cleanup in workQueue.Reader.ReadAllAsync(cancellationToken))
            {
                await Bot.DeleteOldMessagesAsync(cleanup.ChannelId, cleanup.MaxAge, cancellationToken);
            }

            try
            {
                await Task.Delay(100, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                // ignored
            }
        }
        log.LogDebug("Work queue handler stopped");
    }

    /// <summary>
    ///     Emits <see cref="ChannelCleanupConfig.ChannelCleanup">work</see> to the <see cref="workQueue">queue</see> based on the <see cref="scheduledTasks" />.
    /// </summary>
    private async Task RunCleanupProducer(CancellationToken cancellationToken)
    {
        static DateTime GenerateNextOccurrence(ChannelCleanupConfig.ChannelCleanup definition)
        {
            return CrontabSchedule.Parse(definition.Schedule).GetNextOccurrence(DateTime.UtcNow);
        }

        static DateTime AddOrUpdateScheduleForCleanupDefinition(ConcurrentDictionary<ChannelCleanupConfig.ChannelCleanup, DateTime> scheduledTasks, ChannelCleanupConfig.ChannelCleanup definition)
        {
            return scheduledTasks.AddOrUpdate(definition, GenerateNextOccurrence, static (t, _) => GenerateNextOccurrence(t));
        }

        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(1000));
        while (!(serviceCancellationSource?.IsCancellationRequested ?? false))
        {
            await timer.WaitForNextTickAsync(cancellationToken);

            // Check if cleanup definitions indicates that task should run now, keeping track if task already ran.
            foreach (ChannelCleanupConfig.ChannelCleanup cleanupDefinition in CleanupDefinitions)
            {
                if (serviceCancellationSource?.IsCancellationRequested == true)
                {
                    break;
                }

                if (!scheduledTasks.TryGetValue(cleanupDefinition, out DateTime scheduledTime))
                {
                    scheduledTime = AddOrUpdateScheduleForCleanupDefinition(scheduledTasks, cleanupDefinition);
                }

                // If scheduled time is in the past, queue for immediate run and calc the next occurence.
                if ((scheduledTime - DateTime.UtcNow).Ticks < 0)
                {
                    await workQueue.Writer.WriteAsync(cleanupDefinition, cancellationToken);
                    AddOrUpdateScheduleForCleanupDefinition(scheduledTasks, cleanupDefinition);
                }
            }
        }
        log.LogDebug("Scheduler stopped");
    }

    /// <summary>
    ///     Gets the cleanup tasks as provided by configuration.
    /// </summary>
    public IEnumerable<ChannelCleanupConfig.ChannelCleanup> CleanupDefinitions =>
        options.CurrentValue.CleanupDefinitions ?? ArraySegment<ChannelCleanupConfig.ChannelCleanup>.Empty;

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        serviceCancellationSource?.Cancel();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        configChangeSubscription?.Dispose();
    }
}
