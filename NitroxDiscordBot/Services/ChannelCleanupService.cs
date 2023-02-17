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
public class ChannelCleanupService : DiscordBotService
{
    /// <summary>
    ///     Gets a list of cleanup tasks mapped to a cleanup definition in the config file. Each config entry should have only 1 (future) cleanup task.
    /// </summary>
    private readonly ConcurrentDictionary<ChannelCleanupConfig.ChannelCleanup, DateTime> scheduledTasks = new();

    private readonly IOptionsMonitor<ChannelCleanupConfig> options;

    /// <summary>
    ///     Cleanup tasks that are submitted to, based on the <see cref="scheduledTasks" />.
    /// </summary>
    private Channel<ChannelCleanupConfig.ChannelCleanup> workQueue;

    private CancellationTokenSource serviceCancellation;

    public ChannelCleanupService(NitroxBotService bot, IOptionsMonitor<ChannelCleanupConfig> options, ILogger<ChannelCleanupService> log) : base(bot, log)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));

        RegisterDisposable(options.AsObservable().Throttle(TimeSpan.FromSeconds(2)).StartWith(options.CurrentValue).Subscribe(OptionsChanged));
    }

    private void OptionsChanged(ChannelCleanupConfig obj)
    {
        scheduledTasks.Clear();

        var definitions = CleanupDefinitions.Count();
        if (definitions < 1)
        {
            Log.LogInformation("Cleanup service disabled");
        }
        else
        {
            var cleanupTaskSummary = string.Join(Environment.NewLine, CleanupDefinitions.Select(t => t.ToString()));
            Log.LogInformation("Found {Count} cleanup tasks:{NewLine}{CleanupTasks}", definitions, Environment.NewLine, cleanupTaskSummary);
        }
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        if (!serviceCancellation?.IsCancellationRequested ?? false)
        {
            throw new InvalidOperationException($"Attempted to start {nameof(ChannelCleanupService)} while it is already running");
        }
        serviceCancellation = new();

        workQueue = workQueue == null || workQueue.Reader.Completion.IsCompleted ? Channel.CreateBounded<ChannelCleanupConfig.ChannelCleanup>(20) : workQueue;
        var workCancelToken = serviceCancellation.Token;
        RegisterDisposable(workCancelToken.Register(() => workQueue.Writer.Complete()), true);
        _ = Task.Run(() => RunCleanupProducer(workCancelToken), cancellationToken);
        _ = Task.Run(() => RunCleanupConsumer(workCancelToken), cancellationToken);

        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        serviceCancellation.Cancel();
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Emits <see cref="ChannelCleanupConfig.ChannelCleanup">work</see> to the <see cref="workQueue">queue</see> based on the <see cref="scheduledTasks" />.
    /// </summary>
    /// <param name="cancellationToken"></param>
    private async Task RunCleanupProducer(CancellationToken cancellationToken)
    {
        static DateTime GenerateNextOccurrence(ChannelCleanupConfig.ChannelCleanup definition)
        {
            return CrontabSchedule.Parse(definition.Schedule).GetNextOccurrence(DateTime.UtcNow);
        }

        static DateTime AddOrUpdateScheduleForCleanupDefinition(
            ConcurrentDictionary<ChannelCleanupConfig.ChannelCleanup, DateTime> scheduledTasks,
            ChannelCleanupConfig.ChannelCleanup definition)
        {
            return scheduledTasks.AddOrUpdate(definition, GenerateNextOccurrence, static (t, _) => GenerateNextOccurrence(t));
        }

        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(1000));
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // ignored
            }

            // Check if cleanup definitions indicates that task should run now, keeping track if task already ran.
            foreach (ChannelCleanupConfig.ChannelCleanup cleanupDefinition in CleanupDefinitions)
            {
                if (cancellationToken.IsCancellationRequested)
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

        Log.LogInformation("Work producer stopped");
    }

    /// <summary>
    ///     Processes the <see cref="workQueue">queued</see> cleanup work.
    /// </summary>
    private async Task RunCleanupConsumer(CancellationToken cancellationToken = default)
    {
        try
        {
            await foreach (ChannelCleanupConfig.ChannelCleanup cleanup in workQueue.Reader.ReadAllAsync(cancellationToken).WithCancellation(cancellationToken))
            {
                await Bot.DeleteOldMessagesAsync(cleanup.ChannelId, cleanup.MaxAge, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // ignored
        }

        Log.LogInformation("Work consumer stopped");
    }

    /// <summary>
    ///     Gets the cleanup tasks as provided by configuration.
    /// </summary>
    public IEnumerable<ChannelCleanupConfig.ChannelCleanup> CleanupDefinitions =>
        options.CurrentValue.CleanupDefinitions ?? ArraySegment<ChannelCleanupConfig.ChannelCleanup>.Empty;
}
