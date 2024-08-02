using System.Collections.Concurrent;
using Discord;
using Discord.Rest;
using Discord.WebSocket;

namespace NitroxDiscordBot.Core;

/// <summary>
///     Handle which allows continuous async operations over an original interaction.
/// </summary>
public record InteractionHandle : IDisposable
{
    private static readonly object registeryLock = new();

    /// <summary>
    ///     Registery of all interaction handles which can receive signals and are currently active.
    /// </summary>
    private static readonly Dictionary<string, InteractionHandle> registery = [];

    private readonly ConcurrentQueue<TaskCompletionSource<Capture>> captureQueue = [];

    public InteractionHandle()
    {
        lock (registeryLock)
        {
            registery.Add(HandleId, this);
        }
    }

    public string HandleId { get; init; } = Guid.NewGuid().ToString("N");

    public void Dispose()
    {
        lock (registeryLock)
        {
            registery.Remove(HandleId);
        }
        foreach (TaskCompletionSource<Capture> taskSource in captureQueue)
        {
            taskSource.TrySetCanceled();
        }
        captureQueue.Clear();
    }

    /// <summary>
    ///     Waits for an interaction to happen on the original interaction source as tracked by this handle.
    /// </summary>
    /// <remarks>
    ///     See Discord component interaction flow chart:
    ///     https://docs.discordnet.dev/faq/int_framework/images/response-scheme-component.svg
    /// </remarks>
    public Task<Capture> CaptureAsync(TimeSpan timeout = default)
    {
        TaskCompletionSource<Capture> taskSource = new();
        if (timeout != default)
        {
            CancellationTokenSource cts = new(timeout);
            cts.Token.Register(() =>
            {
                taskSource.TrySetCanceled();
                cts.Dispose();
            }, false);
        }
        captureQueue.Enqueue(taskSource);
        return taskSource.Task;
    }

    /// <summary>
    ///     Signals that a new interaction on an interaction component just happened and returns the response to be handled by
    ///     Discord.
    /// </summary>
    public static Task Signal(string handleId, SocketMessageComponent interaction)
    {
        InteractionHandle handle;
        lock (registeryLock)
        {
            if (!registery.TryGetValue(handleId, out handle)) return interaction.DeferAsync(true);
        }
        if (handle.captureQueue.TryDequeue(out TaskCompletionSource<Capture> taskSource))
        {
            ComponentCapture componentCapture = new()
            {
                Handle = handle,
                Interaction = interaction
            };
            taskSource.TrySetResult(componentCapture);
            return componentCapture.DiscordResponseTask;
        }
        return interaction.DeferAsync(true);
    }

    public static Task Signal(string handleId, SocketModal modal)
    {
        InteractionHandle handle;
        lock (registeryLock)
        {
            if (!registery.TryGetValue(handleId, out handle)) return modal.DeferAsync(true);
        }
        if (handle.captureQueue.TryDequeue(out TaskCompletionSource<Capture> taskSource))
        {
            ModalCapture modalCapture = new()
            {
                Handle = handle,
                Modal = modal
            };
            taskSource.TrySetResult(modalCapture);
            return modalCapture.DiscordResponseTask;
        }
        return modal.DeferAsync(true);
    }

    public string CreateTrackedCustomId(string customId)
    {
        return $"{HandleId}-{customId}";
    }

    /// <summary>
    ///     Tracking object used for responding on interactions in the manner Discord expects.
    /// </summary>
    public abstract record Capture : IDisposable
    {
        protected TaskCompletionSource<Task> DiscordResponseTaskSource = new();
        public Task DiscordResponseTask => DiscordResponseTaskSource.Task;
        public InteractionHandle Handle { get; init; }

        public abstract void Dispose();

        /// <summary>
        ///     Returns the task as response to an interaction on Discord, and waits until it completes.
        /// </summary>
        protected async Task RespondToDiscordAsync(Task task)
        {
            DiscordResponseTaskSource.TrySetResult(task);
            await task;
            DiscordResponseTaskSource = new TaskCompletionSource<Task>();
        }
    }

    /// <inheritdoc />
    public record ComponentCapture : Capture
    {
        public SocketMessageComponent Interaction { get; init; }
        public ReadOnlySpan<char> ComponentCustomId => Interaction.Data.GetCapturedCustomId();

        public override void Dispose()
        {
            DiscordResponseTaskSource.TrySetResult(Interaction.DeferAsync(true));
        }

        public async Task RespondDeferAsync()
        {
            await RespondToDiscordAsync(Interaction.DeferAsync(true));
        }

        public async Task FollowupAsync(string message)
        {
            await Interaction.FollowupAsync(message, ephemeral: true);
        }

        public async Task DeleteOriginalAsync()
        {
            await Interaction.DeleteOriginalResponseAsync();
        }

        public async Task<(SocketModalData, ModalCapture)> RespondModalAsync(Modal modal)
        {
            modal.CustomId = Handle.CreateTrackedCustomId(modal.CustomId);
            await RespondToDiscordAsync(Interaction.RespondWithModalAsync(modal));
            if (await Handle.CaptureAsync() is not ModalCapture modalCapture)
            {
                return default;
            }
            using (modalCapture)
            {
                return (modalCapture.Modal.Data, modalCapture);
            }
        }

        public async Task<(TEnum, ComponentCapture)> RespondFollowupSelectAsync<TEnum>(string message = null, string placeholder = null) where TEnum : struct, Enum
        {
            List<SelectMenuOptionBuilder> options = [];
            foreach (TEnum value in Enum.GetValues<TEnum>())
            {
                string enumLabel = value.ToString();
                options.Add(new SelectMenuOptionBuilder()
                    .WithLabel(enumLabel)
                    .WithValue(enumLabel));
            }

            MessageComponent filterTypeSelect = new ComponentBuilder()
                .WithSelectMenu(Handle.CreateTrackedCustomId(typeof(TEnum).Name.ToLowerInvariant()), options, placeholder: placeholder).Build();
            RestFollowupMessage filterTypeFollowUp = await Interaction.FollowupAsync(message, components: filterTypeSelect, ephemeral: true);
            if (await Handle.CaptureAsync(TimeSpan.FromMinutes(15)) is not ComponentCapture filterTypeCapture)
            {
                return default;
            }
            try
            {
                if (Enum.TryParse(filterTypeCapture.Interaction.Data.Values.FirstOrDefault() ?? "", true,
                        out TEnum result))
                {
                    return (result, filterTypeCapture);
                }
            }
            finally
            {
                await filterTypeFollowUp.DeleteAsync();
            }
            return default;
        }
    }

    /// <inheritdoc />
    public record ModalCapture : Capture
    {
        public SocketModal Modal { get; init; }
        public ReadOnlySpan<char> Id => Modal.Data.GetCapturedCustomId();

        public override void Dispose()
        {
            DiscordResponseTaskSource.TrySetResult(Modal.DeferAsync(true));
        }
    }
}