namespace NitroxDiscordBot.Services.Ntfy;

public interface INtfyService
{
    Uri? Url { get; }
    Task SendMessageAsync(string topic, string message, string title, string urlLabel, string url);
    Task<bool> IsAvailable();

    static string AsTopicName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return value.Replace(" ", "");
    }
}