namespace NitroxDiscordBot.Services.Ntfy;

public interface INtfyService
{
    public Uri Url { get; }
    Task SendMessageAsync(string topic, string message, string title, string urlLabel, string url);
    Task<bool> IsAvailable();

    static string AsTopicName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Replace(" ", "");
    }
}