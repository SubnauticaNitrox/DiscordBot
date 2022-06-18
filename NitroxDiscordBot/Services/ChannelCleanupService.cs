using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Timers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCrontab;
using NitroxDiscordBot.Configuration;
using Timer = System.Timers.Timer;

namespace NitroxDiscordBot.Services;

/// <summary>
///     Cleans up "old" messages from Discord channels.
/// </summary>
public class ChannelCleanupService : IHostedService, IDisposable
{
    private readonly NitroxBotService bot;
    private readonly ILogger<ChannelCleanupService> log;
    private readonly Timer timer;

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
    private readonly ConcurrentQueue<ChannelCleanupConfig.ChannelCleanup> queue = new();

    public ChannelCleanupService(NitroxBotService bot, IOptionsMonitor<ChannelCleanupConfig> options, ILogger<ChannelCleanupService> log)
    {
        this.bot = bot;
        this.log = log;
        this.options = options;
        timer = new Timer(1000);
        timer.AutoReset = true;
        timer.Elapsed += TimerOnElapsed;

        OptionsChanged(options.CurrentValue);
        configChangeSubscription = options.CreateObservable().Throttle(TimeSpan.FromSeconds(2)).Subscribe(OptionsChanged);
    }

    private void OptionsChanged(ChannelCleanupConfig obj)
    {
        schedules.Clear();

        var taskCount = obj.CleanupTasks?.Count() ?? 0;
        var turnedOff = timer.Enabled && taskCount < 1;
        timer.Enabled = taskCount >= 1;
        if (turnedOff)
        {
            log.LogInformation("Cleanup service disabled");
        }
        if (!timer.Enabled)
        {
            return;
        }

        log.LogInformation($"Found {taskCount} cleanup tasks");
        foreach (ChannelCleanupConfig.ChannelCleanup task in obj.CleanupTasks!)
        {
            log.LogInformation(task.ToString());
        }
    }

    private void TimerOnElapsed(object? sender, ElapsedEventArgs e)
    {
        static DateTime GenerateNextOccurrence(ChannelCleanupConfig.ChannelCleanup task)
        {
            return CrontabSchedule.Parse(task.Schedule).GetNextOccurrence(DateTime.UtcNow);
        }

        // Check if schedule indicates that task should run now, keep track if task already ran.
        IEnumerable<ChannelCleanupConfig.ChannelCleanup> cleanupTasks = options.CurrentValue.CleanupTasks ?? ArraySegment<ChannelCleanupConfig.ChannelCleanup>.Empty;
        foreach (ChannelCleanupConfig.ChannelCleanup task in cleanupTasks)
        {
            if (serviceCancellationSource?.IsCancellationRequested == true)
            {
                break;
            }

            if (!schedules.TryGetValue(task, out DateTime scheduledTime))
            {
                scheduledTime = schedules.AddOrUpdate(task, GenerateNextOccurrence, (t, _) => GenerateNextOccurrence(t));
            }

            // If scheduled time is in the past, run it now and calc the next occurence.
            if ((scheduledTime - DateTime.UtcNow).Ticks < 0)
            {
                queue.Enqueue(task);
                schedules.AddOrUpdate(task, GenerateNextOccurrence, (t, _) => GenerateNextOccurrence(t));
            }
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        serviceCancellationSource = new CancellationTokenSource();
        await bot.WaitForReadyAsync(cancellationToken);
        if (!bot.IsConnected)
        {
            throw new Exception("Discord bot has not started yet");
        }

        timer.Start();

        // Fire and forget task that executes the cleanup, which is cancellable via cancel token.
        _ = Task.Run(async () =>
            {
                while (!serviceCancellationSource.IsCancellationRequested)
                {
                    while (!queue.IsEmpty)
                    {
                        if (queue.TryDequeue(out ChannelCleanupConfig.ChannelCleanup? cleanup))
                        {
                            await bot.DeleteOldMessagesAsync(cleanup.ChannelId, cleanup.MaxAge, serviceCancellationSource.Token);
                        }
                    }

                    try
                    {
                        await Task.Delay(100, serviceCancellationSource.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        // ignored
                    }
                }
            }, serviceCancellationSource.Token)
            .ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        timer.Stop();
        serviceCancellationSource?.Cancel();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        timer.Dispose();
        configChangeSubscription?.Dispose();
    }
}