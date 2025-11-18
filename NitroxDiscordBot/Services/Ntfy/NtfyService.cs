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
        NtfyConfig configValue = config.Value;
        ArgumentException.ThrowIfNullOrWhiteSpace(configValue.Url, nameof(configValue.Url));

        this.client = client;
        client.BaseAddress = new Uri(configValue.Url);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(nameof(NitroxDiscordBot),
            typeof(NtfyService).Assembly.GetName().Version?.ToString() ?? "1.0.0.0"));
    }

    public Uri? Url => client.BaseAddress;

    public async Task SendMessageAsync(string topic, string message, string title, string urlLabel, string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(urlLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        StringContent content = new(message);
        content.Headers.Add("Title", title);
        content.Headers.Add("Actions", $"view, {urlLabel}, {url}");
        using HttpResponseMessage response = await client.PostAsync(topic, content);
    }

    public async Task<bool> IsAvailable()
    {
        try
        {
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
            using HttpRequestMessage request = new(HttpMethod.Head, Url);
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}