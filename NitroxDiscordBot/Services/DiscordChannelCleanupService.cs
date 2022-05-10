using System.Collections.Concurrent;
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
public class DiscordChannelCleanupService : IHostedService, IDisposable
{
    private readonly NitroxBotService bot;
    private readonly ILogger<DiscordChannelCleanupService> log;
    private readonly Timer timer;

    /// <summary>
    ///     Cleanup schedules that are checked by this service.
    /// </summary>
    private readonly ConcurrentDictionary<DiscordChannelCleanupConfig.ChannelCleanup, DateTime> schedules = new();

    /// <summary>
    ///     Used to neatly exit this service.
    /// </summary>
    private CancellationTokenSource? serviceCancellationSource;

    private readonly IDisposable onChangeListener;
    private readonly IOptionsMonitor<DiscordChannelCleanupConfig> options;

    /// <summary>
    ///     Cleanup tasks that are submitted to, based on the <see cref="schedules" />.
    /// </summary>
    private readonly ConcurrentQueue<DiscordChannelCleanupConfig.ChannelCleanup> queue = new();

    public DiscordChannelCleanupService(NitroxBotService bot, IOptionsMonitor<DiscordChannelCleanupConfig> options, ILogger<DiscordChannelCleanupService> log)
    {
        this.bot = bot;
        this.log = log;
        this.options = options;
        timer = new Timer(1000);
        timer.AutoReset = true;
        timer.Elapsed += TimerOnElapsed;

        OptionsChanged(options.CurrentValue);
        onChangeListener = options.OnChange(OptionsChanged);
    }

    private void OptionsChanged(DiscordChannelCleanupConfig obj)
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
        foreach (DiscordChannelCleanupConfig.ChannelCleanup task in obj.CleanupTasks!)
        {
            log.LogInformation(task.ToString());
        }
    }

    private void TimerOnElapsed(object? sender, ElapsedEventArgs e)
    {
        static DateTime GenerateNextOccurrence(DiscordChannelCleanupConfig.ChannelCleanup task)
        {
            return CrontabSchedule.Parse(task.Schedule).GetNextOccurrence(DateTime.UtcNow);
        }

        // Check if schedule indicates that task should run now, keep track if task already ran.
        foreach (DiscordChannelCleanupConfig.ChannelCleanup task in options.CurrentValue.CleanupTasks!)
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
        serviceCancellationSource = new();
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
                        if (queue.TryDequeue(out DiscordChannelCleanupConfig.ChannelCleanup? cleanup))
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
        onChangeListener.Dispose();
    }
}