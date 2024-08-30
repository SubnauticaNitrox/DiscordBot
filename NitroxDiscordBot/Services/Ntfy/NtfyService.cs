using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using NitroxDiscordBot.Configuration;

namespace NitroxDiscordBot.Services.Ntfy;

/// <summary>
///     Service providing Ntfy integration. See <a href="https://github.com/binwiederhier/ntfy" /> for more info.
/// </summary>
public sealed class NtfyService : INtfyService
{
    private readonly HttpClient client;

    public NtfyService(HttpClient client, IOptions<NtfyConfig> config)
    {
        ArgumentNullException.ThrowIfNull(client, nameof(client));
        ArgumentNullException.ThrowIfNull(config, nameof(config));
        this.client = client;
        string url = config.Value.Url;
        if (string.IsNullOrEmpty(url))
        {
            throw new ArgumentException($"{nameof(Ntfy)} {nameof(url)} is empty");
        }
        client.BaseAddress = Url = new Uri(url);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(nameof(NitroxDiscordBot),
            typeof(NtfyService).Assembly.GetName().Version?.ToString() ?? "1.0.0.0"));
    }

    public Uri Url { get; }

    public Task SendMessageAsync(string topic, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        using StringContent content = new(message);
        return client.PostAsync(topic, content);
    }

    public Task SendMessageWithUrl(string topic, string message, string urlLabel, string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(urlLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        using StringContent content = new(message);
        content.Headers.Add("Actions", $"view, {urlLabel}, {url}");
        return client.PostAsync(topic, content);
    }

    public Task SendMessageWithTitleAndUrl(string topic, string title, string message, string urlLabel, string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(urlLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        using StringContent content = new(message);
        content.Headers.Add("Title", title);
        content.Headers.Add("Actions", $"view, {urlLabel}, {url}");
        return client.PostAsync(topic, content);
    }
}