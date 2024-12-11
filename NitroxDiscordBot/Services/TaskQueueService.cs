using System.Threading.Channels;

namespace NitroxDiscordBot.Services;

/// <summary>
///     Service which tracks tasks that were fired but should not be forgotten. This means that remaining tasks will be
///     awaited when the service stops.
/// </summary>
public sealed class TaskQueueService : IHostedService
{
    private readonly CancellationTokenSource cts = new();
    private readonly ILogger<TaskQueueService> log;
    private readonly Channel<Task> tasks = Channel.CreateUnbounded<Task>();

    public TaskQueueService(ILogger<TaskQueueService> log)
    {
        this.log = log;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    Task task = await tasks.Reader.ReadAsync(cancellationToken);
                    await task;
                }
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Task.FromCanceled(cancellationToken);
        }
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await cts.CancelAsync();
            tasks.Writer.TryComplete();
            await foreach (Task task in tasks.Reader.ReadAllAsync(cancellationToken))
            {
                await task;
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error occured while trying to stop service");
        }
    }

    public async Task EnqueueAsync(Task task)
    {
        await tasks.Writer.WriteAsync(task);
    }
}