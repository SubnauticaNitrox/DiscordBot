using System.Reactive.Linq;
using System.Threading.Channels;
using Discord;
using Microsoft.Extensions.Options;
using NitroxDiscordBot.Configuration;
using NitroxDiscordBot.Core;

namespace NitroxDiscordBot.Services;

/// <summary>
///     Adds and maintains message-of-the-day channels based on the <see cref="MotdConfig" />.
/// </summary>
public class MotdService : DiscordBotHostedService
{
    private readonly Channel<MotdConfig.ChannelMotd> channelOfMotds = Channel.CreateUnbounded<MotdConfig.ChannelMotd>();

    public MotdService(NitroxBotService bot, IOptionsMonitor<MotdConfig> options, ILogger<MotdService> log) : base(bot, log)
    {
        RegisterDisposable(options.AsObservable().Throttle(TimeSpan.FromSeconds(2)).StartWith(options.CurrentValue).Subscribe(config => _ = OptionsChanged(config)));
    }

    private async Task OptionsChanged(MotdConfig config)
    {
        if (config.ChannelMotds == null)
        {
            return;
        }

        foreach (MotdConfig.ChannelMotd motd in config.ChannelMotds)
        {
            await channelOfMotds.Writer.WriteAsync(motd);
        }
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(async () => await RunMotdConsumer(), cancellationToken);
        return Task.CompletedTask;
    }

    private async Task RunMotdConsumer()
    {
        try
        {
            await foreach (MotdConfig.ChannelMotd motd in channelOfMotds.Reader.ReadAllAsync())
            {
                if (motd.Messages == null)
                {
                    continue;
                }

                int index = 0;
                foreach (MotdConfig.MotdMessage message in motd.Messages)
                {
                    EmbedBuilder embed = new EmbedBuilder()
                        .WithTitle(message.Title)
                        .WithDescription(message.Description)
                        .WithUrl(message.Url)
                        .WithThumbnailUrl(message.ThumbnailUrl)
                        .WithImageUrl(message.ImageUrl)
                        .WithFooter(message.Footer, message.FooterIconUrl)
                        .WithColor(message.Color.ParseHexToUint())
                        .WithFields(message.Fields?.Select(f => new EmbedFieldBuilder().WithName(f.Name).WithValue(f.Content).WithIsInline(f.IsInline)) ??
                                    ArraySegment<EmbedFieldBuilder>.Empty);
                    await Bot.CreateOrUpdateMessage(motd.ChannelId, index, embed.Build());
                    Log.LogInformation("Added/updated MOTD in channel #{ChannelId} at index {Index}", motd.ChannelId, index);
                    index++;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
    }
}
