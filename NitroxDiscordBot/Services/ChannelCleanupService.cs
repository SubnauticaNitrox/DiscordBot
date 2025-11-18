using System.Collections.Concurrent;
using System.Threading.Channels;
using Cronos;
using Microsoft.EntityFrameworkCore;
using NitroxDiscordBot.Core;
using NitroxDiscordBot.Db;
using NitroxDiscordBot.Db.Models;
using Polly;
using Polly.Retry;

namespace NitroxDiscordBot.Services;

/// <summary>
///     Cleans up "old" messages from Discord channels.
/// </summary>
public class ChannelCleanupService : DiscordBotHostedService
{
    private readonly BotContext db;
    private readonly ResiliencePipeline resilience;

    /// <summary>
    ///     Gets a list of cleanup tasks mapped to a cleanup definition in the config file. Each config entry should have only
    ///     1 (future) cleanup task.
    /// </summary>
    private readonly ConcurrentDictionary<Cleanup, DateTime> scheduledTasks = [];

    private Task consumerTask = null!;
    private Task producerTask = null!;
    private CancellationTokenSource? serviceCancellation;

    /// <summary>
    ///     Cleanup tasks that are submitted to, based on the <see cref="scheduledTasks" />.
    /// </summary>
    private Channel<Cleanup>? workQueue;

    public ChannelCleanupService(NitroxBotService bot,
        BotContext db,
        ILogger<ChannelCleanupService> log) : base(bot, log)
    {
        ArgumentNullException.ThrowIfNull(db);
        this.db = db;
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
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        if (!serviceCancellation?.IsCancellationRequested ?? false)
            throw new InvalidOperationException(
                $"Attempted to start {nameof(ChannelCleanupService)} while it is already running");

        serviceCancellation = new CancellationTokenSource();

        workQueue = workQueue == null || workQueue.Reader.Completion.IsCompleted
            ? Channel.CreateBounded<Cleanup>(20)
            : workQueue;
        CancellationToken workCancelToken = serviceCancellation.Token;
        RegisterDisposable(workCancelToken.Register(() => workQueue.Writer.Complete()), true);
        producerTask = Task.Run(() => RunCleanupProducerAsync(workCancelToken), cancellationToken);
        consumerTask = Task.Run(() => RunCleanupConsumerAsync(workCancelToken), cancellationToken);

        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        serviceCancellation?.Cancel();
        return Task.WhenAll(producerTask, consumerTask);
    }

    /// <summary>
    ///     Emits <see cref="ChannelCleanupConfig.ChannelCleanup">work</see> to the <see cref="workQueue">queue</see> based on
    ///     the <see cref="scheduledTasks" />.
    /// </summary>
    /// <param name="cancellationToken"></param>
    private async Task RunCleanupProducerAsync(CancellationToken cancellationToken)
    {
        static DateTime GenerateNextOccurrence(Cleanup definition)
        {
            return CronExpression.Parse(definition.CronSchedule).GetNextOccurrence(DateTime.UtcNow) ??
                   throw new Exception(
                       $"Cron expression '{definition.CronSchedule}' does not have an occurrence after {DateTime.UtcNow}; the service will stop.");
        }

        static DateTime AddOrUpdateScheduleForCleanupDefinition(
            ConcurrentDictionary<Cleanup, DateTime> scheduledTasks,
            Cleanup definition)
        {
            return scheduledTasks.AddOrUpdate(definition, GenerateNextOccurrence,
                static (t, _) => GenerateNextOccurrence(t));
        }

        // TODO: Don't use periodic timer but calculate next first schedule to run and wait for that.
        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(5000));
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
            try
            {
                await foreach (Cleanup cleanupDefinition in db.Cleanups
                                   .AsNoTracking()
                                   .AsAsyncEnumerable()
                                   .WithCancellation(cancellationToken))
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    if (!scheduledTasks.TryGetValue(cleanupDefinition, out DateTime scheduledTime))
                    {
                        scheduledTime = AddOrUpdateScheduleForCleanupDefinition(scheduledTasks, cleanupDefinition);
                    }

                    // If scheduled time is in the past, queue for immediate run and calc the next occurence.
                    if ((scheduledTime - DateTime.UtcNow).Ticks < 0)
                    {
                        await workQueue!.Writer.WriteAsync(cleanupDefinition, cancellationToken);
                        AddOrUpdateScheduleForCleanupDefinition(scheduledTasks, cleanupDefinition);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
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
            await foreach (Cleanup cleanup in workQueue!.Reader.ReadAllAsync(cancellationToken))
            {
                await resilience.ExecuteAsync(async (context, ct) =>
                {
                    (NitroxBotService bot, Cleanup cleanupItem) = context;
                    await bot.DeleteOldMessagesAsync(cleanupItem.ChannelId, cleanupItem.AgeThreshold, ct);
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