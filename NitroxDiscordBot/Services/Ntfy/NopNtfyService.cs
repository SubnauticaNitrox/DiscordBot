namespace NitroxDiscordBot.Services.Ntfy;

/// <summary>
///     Ntfy service that does nothing. Used when the configuration for Ntfy is invalid.
/// </summary>
public sealed class NopNtfyService : INtfyService
{
    public Uri? Url => null;

    public Task SendMessageAsync(string topic, string message, string title, string urlLabel, string url)
    {
        return Task.CompletedTask;
    }

    public Task<bool> IsAvailable()
    {
        return Task.FromResult(false);
    }
}