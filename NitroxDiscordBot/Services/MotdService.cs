using System.Reactive.Linq;
using Discord;
using Microsoft.Extensions.Options;
using NitroxDiscordBot.Configuration;
using NitroxDiscordBot.Core;

namespace NitroxDiscordBot.Services;

/// <summary>
///     Adds and maintains message-of-the-day channels based on the <see cref="MotdConfig" />.
/// </summary>
public class MotdService : BaseDiscordBotService
{
    private readonly ILogger<MotdService> log;
    private readonly IObservable<MotdConfig> configChangedObservable;
    private IDisposable? configChangeSubscription;

    private readonly IOptionsMonitor<MotdConfig> options;

    public MotdService(NitroxBotService bot, IOptionsMonitor<MotdConfig> options, ILogger<MotdService> log) : base(bot)
    {
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
                    .WithColor(message.Color.HexToUint())
                    .WithFields(message.Fields?.Select(f => new EmbedFieldBuilder().WithName(f.Name).WithValue(f.Content).WithIsInline(f.IsInline)) ??
                                ArraySegment<EmbedFieldBuilder>.Empty);
                await Bot.CreateOrUpdateMessage(motd.ChannelId, index, embed.Build());
                log.LogInformation($"Added/updated MOTD in channel #{motd.ChannelId} at index {index}");
                index++;
            }
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await OptionsChanged(options.CurrentValue);
        configChangeSubscription = configChangedObservable.Subscribe(config => _ = OptionsChanged(config));
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        configChangeSubscription?.Dispose();
        return Task.CompletedTask;
    }
}