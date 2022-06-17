using System.Reactive.Linq;
using Discord;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NitroxDiscordBot.Configuration;

namespace NitroxDiscordBot.Services;

/// <summary>
///     Adds and maintains message-of-the-day channels based on the <see cref="MotdConfig" />.
/// </summary>
public class MotdService : IHostedService
{
    private readonly NitroxBotService bot;
    private readonly ILogger<MotdService> log;
    private readonly IObservable<MotdConfig> configChangedObservable;
    private IDisposable? configChangeSubscription;

    private readonly IOptionsMonitor<MotdConfig> options;

    public MotdService(NitroxBotService bot, IOptionsMonitor<MotdConfig> options, ILogger<MotdService> log)
    {
        this.bot = bot;
        this.log = log;
        this.options = options;
        configChangedObservable = options.CreateObservable().Throttle(TimeSpan.FromSeconds(2));
    }

    private async Task OptionsChanged(MotdConfig config)
    {
        if (config.ChannelMotds == null)
        {
            return;
        }

        foreach (MotdConfig.ChannelMotd motd in config.ChannelMotds)
        {
            if (motd.Messages == null)
            {
                continue;
            }

            var index = 0;
            foreach (MotdConfig.MotdMessage message in motd.Messages)
            {
                EmbedBuilder embed = new EmbedBuilder()
                    .WithTitle(message.Title)
                    .WithDescription(message.Description)
                    .WithUrl(message.Url)
                    .WithThumbnailUrl(message.ThumbnailUrl)
                    .WithImageUrl(message.ImageUrl)
                    .WithFooter(message.Footer, message.FooterIconUrl)
                    .WithFields(message.Fields?.Select(f => new EmbedFieldBuilder().WithName(f.Name).WithValue(f.Content).WithIsInline(f.IsInline)) ??
                                ArraySegment<EmbedFieldBuilder>.Empty);
                await bot.CreateOrUpdateMessage(motd.ChannelId, index, embed.Build());
                log.LogInformation($"Added/updated MOTD in channel #{motd.ChannelId} at index {index}");
                index++;
            }
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await bot.WaitForReadyAsync(cancellationToken);
        await OptionsChanged(options.CurrentValue);
        configChangeSubscription = configChangedObservable.Subscribe(config => _ = OptionsChanged(config));
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        configChangeSubscription?.Dispose();
        return Task.CompletedTask;
    }
}