using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Threading.Channels;
using Cronos;
using Microsoft.Extensions.Options;
using NitroxDiscordBot.Configuration;
using NitroxDiscordBot.Core;

namespace NitroxDiscordBot.Services;

/// <summary>
///     Cleans up "old" messages from Discord channels.
/// </summary>
public class ChannelCleanupService : DiscordBotHostedService
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

    private Task producerTask;
    private Task consumerTask;
    private CancellationTokenSource serviceCancellation;

    public ChannelCleanupService(NitroxBotService bot, IOptionsMonitor<ChannelCleanupConfig> options, ILogger<ChannelCleanupService> log) : base(bot, log)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));

        RegisterDisposable(options.AsObservable().Throttle(TimeSpan.FromSeconds(2)).StartWith(options.CurrentValue).Subscribe(OptionsChanged));
    }

    private void OptionsChanged(ChannelCleanupConfig obj)
    {
        scheduledTasks.Clear();

        int definitions = CleanupDefinitions.Count();
        if (definitions < 1)
        {
            Log.LogInformation("Cleanup service disabled");
        }
        else
        {
            string cleanupTaskSummary = string.Join(Environment.NewLine, CleanupDefinitions.Select(t => t.ToString()));
            Log.LogInformation("Found {Count} cleanup tasks:{NewLine}{CleanupTasks}", definitions, Environment.NewLine, cleanupTaskSummary);
        }
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        if (!serviceCancellation?.IsCancellationRequested ?? false)
        {
            throw new InvalidOperationException($"Attempted to start {nameof(ChannelCleanupService)} while it is already running");
        }
        serviceCancellation = new CancellationTokenSource();

        workQueue = workQueue == null || workQueue.Reader.Completion.IsCompleted ? Channel.CreateBounded<ChannelCleanupConfig.ChannelCleanup>(20) : workQueue;
        CancellationToken workCancelToken = serviceCancellation.Token;
        RegisterDisposable(workCancelToken.Register(() => workQueue.Writer.Complete()), true);
        producerTask = Task.Run(() => RunCleanupProducerAsync(workCancelToken), cancellationToken);
        consumerTask = Task.Run(() => RunCleanupConsumerAsync(workCancelToken), cancellationToken);

        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        serviceCancellation.Cancel();
        return Task.WhenAll(producerTask, consumerTask);
    }

    /// <summary>
    ///     Emits <see cref="ChannelCleanupConfig.ChannelCleanup">work</see> to the <see cref="workQueue">queue</see> based on the <see cref="scheduledTasks" />.
    /// </summary>
    /// <param name="cancellationToken"></param>
    private async Task RunCleanupProducerAsync(CancellationToken cancellationToken)
    {
        static DateTime GenerateNextOccurrence(ChannelCleanupConfig.ChannelCleanup definition)
        {
            return CronExpression.Parse(definition.Schedule).GetNextOccurrence(DateTime.UtcNow) ??
                   throw new Exception($"Cron expression '{definition.Schedule}' does not have an occurrence after {DateTime.UtcNow}; the service will stop.");
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
                break;
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

        Log.LogDebug("Work producer stopped");
    }

    /// <summary>
    ///     Processes the <see cref="workQueue">queued</see> cleanup work.
    /// </summary>
    private async Task RunCleanupConsumerAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await foreach (ChannelCleanupConfig.ChannelCleanup cleanup in workQueue.Reader.ReadAllAsync(cancellationToken).WithCancellation(cancellationToken))
            {
                int retries = 3;
                while (retries > 0 && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await Bot.DeleteOldMessagesAsync(cleanup.ChannelId, cleanup.MaxAge, cancellationToken);
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log.LogDebug(ex, "Discord API returned an error while trying to delete old messages");
                        retries--;
                        try
                        {
                            await Task.Delay(100, cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // ignored
        }

        Log.LogDebug("Work consumer stopped");
    }

    /// <summary>
    ///     Gets the cleanup tasks as provided by configuration.
    /// </summary>
    public IEnumerable<ChannelCleanupConfig.ChannelCleanup> CleanupDefinitions =>
        options.CurrentValue.CleanupDefinitions ?? ArraySegment<ChannelCleanupConfig.ChannelCleanup>.Empty;
}