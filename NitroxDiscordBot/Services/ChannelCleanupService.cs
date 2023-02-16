using System.Collections.Concurrent;
using System.Reactive.Linq;
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
    ///     Cleanup schedules that are checked by this service.
    /// </summary>
    private readonly ConcurrentDictionary<ChannelCleanupConfig.ChannelCleanup, DateTime> schedules = new();

    /// <summary>
    ///     Used to neatly exit this service.
    /// </summary>
    private CancellationTokenSource? serviceCancellationSource;

    private readonly IOptionsMonitor<ChannelCleanupConfig> options;
    private readonly IDisposable? configChangeSubscription;

    /// <summary>
    ///     Cleanup tasks that are submitted to, based on the <see cref="schedules" />.
    /// </summary>
    private readonly ConcurrentQueue<ChannelCleanupConfig.ChannelCleanup> workQueue = new();

    public ChannelCleanupService(NitroxBotService bot, IOptionsMonitor<ChannelCleanupConfig> options, ILogger<ChannelCleanupService> log) : base(bot)
    {
        this.log = log;
        this.options = options;

        OptionsChanged(options.CurrentValue);
        configChangeSubscription = options.CreateObservable().Throttle(TimeSpan.FromSeconds(2)).Subscribe(OptionsChanged);
    }

    private void OptionsChanged(ChannelCleanupConfig obj)
    {
        schedules.Clear();

        var taskCount = CleanupTasks.Count();
        var turnedOff = taskCount < 1;
        if (turnedOff)
        {
            log.LogInformation("Cleanup service disabled");
        }

        var cleanupTaskSummary = string.Join(Environment.NewLine, CleanupTasks.Select(t => t.ToString()));
        log.LogInformation("Found {Count} cleanup tasks:{NewLine}{CleanupTasks}", taskCount, Environment.NewLine, cleanupTaskSummary);
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        serviceCancellationSource = new CancellationTokenSource();

        _ = Task.Run(async () => await RunQueueHandlerAsync(serviceCancellationSource.Token), cancellationToken);
        _ = Task.Run(async () => await RunSchedulerAsync(serviceCancellationSource.Token), cancellationToken);

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Processes the <see cref="workQueue">queued</see> cleanup work.
    /// </summary>
    private async Task RunQueueHandlerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            while (!workQueue.IsEmpty)
            {
                if (workQueue.TryDequeue(out ChannelCleanupConfig.ChannelCleanup? cleanup))
                {
                    await Bot.DeleteOldMessagesAsync(cleanup.ChannelId, cleanup.MaxAge, cancellationToken);
                }
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
    ///     Emits <see cref="ChannelCleanupConfig.ChannelCleanup">work</see> to the <see cref="workQueue">queue</see> based on the <see cref="schedules" />.
    /// </summary>
    private async Task RunSchedulerAsync(CancellationToken cancellationToken)
    {
        static DateTime GenerateNextOccurrence(ChannelCleanupConfig.ChannelCleanup task)
        {
            return CrontabSchedule.Parse(task.Schedule).GetNextOccurrence(DateTime.UtcNow);
        }

        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(1000));
        while (!(serviceCancellationSource?.IsCancellationRequested ?? false))
        {
            await timer.WaitForNextTickAsync(cancellationToken);

            // Check if schedule indicates that task should run now, keep track if task already ran.
            foreach (ChannelCleanupConfig.ChannelCleanup task in CleanupTasks)
            {
                if (serviceCancellationSource?.IsCancellationRequested == true)
                {
                    break;
                }

                if (!schedules.TryGetValue(task, out DateTime scheduledTime))
                {
                    scheduledTime = schedules.AddOrUpdate(task, GenerateNextOccurrence, static (t, _) => GenerateNextOccurrence(t));
                }

                // If scheduled time is in the past, queue for immediate run and calc the next occurence.
                if ((scheduledTime - DateTime.UtcNow).Ticks < 0)
                {
                    workQueue.Enqueue(task);
                    schedules.AddOrUpdate(task, GenerateNextOccurrence, static (t, _) => GenerateNextOccurrence(t));
                }
            }
        }
        log.LogDebug("Scheduler stopped");
    }

    /// <summary>
    ///     Gets the cleanup tasks as provided by configuration.
    /// </summary>
    public IEnumerable<ChannelCleanupConfig.ChannelCleanup> CleanupTasks => options.CurrentValue.CleanupTasks ?? ArraySegment<ChannelCleanupConfig.ChannelCleanup>.Empty;

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
