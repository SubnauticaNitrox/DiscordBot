using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Threading.Channels;
using Cronos;
using Microsoft.Extensions.Options;
using NitroxDiscordBot.Configuration;
using NitroxDiscordBot.Core;
using Polly;
using Polly.Retry;

namespace NitroxDiscordBot.Services;

/// <summary>
///     Cleans up "old" messages from Discord channels.
/// </summary>
public class ChannelCleanupService : DiscordBotHostedService
{
    private readonly IOptionsMonitor<ChannelCleanupConfig> options;

    private readonly ResiliencePipeline resilience;

    /// <summary>
    ///     Gets a list of cleanup tasks mapped to a cleanup definition in the config file. Each config entry should have only
    ///     1 (future) cleanup task.
    /// </summary>
    private readonly ConcurrentDictionary<ChannelCleanupConfig.ChannelCleanup, DateTime> scheduledTasks = [];

    private Task consumerTask;
    private Task producerTask;
    private CancellationTokenSource serviceCancellation;

    /// <summary>
    ///     Cleanup tasks that are submitted to, based on the <see cref="scheduledTasks" />.
    /// </summary>
    private Channel<ChannelCleanupConfig.ChannelCleanup> workQueue;

    public ChannelCleanupService(NitroxBotService bot,
        IOptionsMonitor<ChannelCleanupConfig> options,
        ILogger<ChannelCleanupService> log) : base(bot, log)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        resilience = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                Delay = TimeSpan.FromSeconds(2),
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            })
            .AddTimeout(TimeSpan.FromSeconds(30))
            .Build();

        RegisterDisposable(options.AsObservable().Throttle(TimeSpan.FromSeconds(2)).StartWith(options.CurrentValue)
            .Subscribe(OptionsChanged));
    }

    /// <summary>
    ///     Gets the cleanup tasks as provided by configuration.
    /// </summary>
    public IEnumerable<ChannelCleanupConfig.ChannelCleanup> Definitions =>
        options.CurrentValue.CleanupDefinitions ?? ArraySegment<ChannelCleanupConfig.ChannelCleanup>.Empty;

    private void OptionsChanged(ChannelCleanupConfig obj)
    {
        scheduledTasks.Clear();

        int definitions = Definitions.Count();
        if (definitions < 1)
        {
            Log.LogInformation("Cleanup service disabled");
        }
        else
        {
            string cleanupTaskSummary = string.Join(Environment.NewLine, Definitions.Select(t => t.ToString()));
            Log.LogInformation("Found {Count} cleanup tasks:{NewLine}{CleanupTasks}", definitions, Environment.NewLine,
                cleanupTaskSummary);
        }
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        if (!serviceCancellation?.IsCancellationRequested ?? false)
            throw new InvalidOperationException(
                $"Attempted to start {nameof(ChannelCleanupService)} while it is already running");

        serviceCancellation = new CancellationTokenSource();

        workQueue = workQueue == null || workQueue.Reader.Completion.IsCompleted
            ? Channel.CreateBounded<ChannelCleanupConfig.ChannelCleanup>(20)
            : workQueue;
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
    ///     Emits <see cref="ChannelCleanupConfig.ChannelCleanup">work</see> to the <see cref="workQueue">queue</see> based on
    ///     the <see cref="scheduledTasks" />.
    /// </summary>
    /// <param name="cancellationToken"></param>
    private async Task RunCleanupProducerAsync(CancellationToken cancellationToken)
    {
        static DateTime GenerateNextOccurrence(ChannelCleanupConfig.ChannelCleanup definition)
        {
            return CronExpression.Parse(definition.Schedule).GetNextOccurrence(DateTime.UtcNow) ??
                   throw new Exception(
                       $"Cron expression '{definition.Schedule}' does not have an occurrence after {DateTime.UtcNow}; the service will stop.");
        }

        static DateTime AddOrUpdateScheduleForCleanupDefinition(
            ConcurrentDictionary<ChannelCleanupConfig.ChannelCleanup, DateTime> scheduledTasks,
            ChannelCleanupConfig.ChannelCleanup definition)
        {
            return scheduledTasks.AddOrUpdate(definition, GenerateNextOccurrence,
                static (t, _) => GenerateNextOccurrence(t));
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
            foreach (ChannelCleanupConfig.ChannelCleanup cleanupDefinition in Definitions)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (!scheduledTasks.TryGetValue(cleanupDefinition, out DateTime scheduledTime))
                    scheduledTime = AddOrUpdateScheduleForCleanupDefinition(scheduledTasks, cleanupDefinition);

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
            await foreach (ChannelCleanupConfig.ChannelCleanup cleanup in workQueue.Reader.ReadAllAsync(
                               cancellationToken))
            {
                await resilience.ExecuteAsync(async (context, ct) =>
                {
                    (NitroxBotService bot, ChannelCleanupConfig.ChannelCleanup cleanupItem) = context;
                    await bot.DeleteOldMessagesAsync(cleanupItem.ChannelId, cleanupItem.MaxAge, ct);
                }, (Bot, cleanup), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // expected to happen
        }
        catch (Exception ex)
        {
            Log.LogCritical(ex, "Unexpected failure");
        }
        finally
        {
            Log.LogDebug("Work consumer stopped");
        }
    }
}