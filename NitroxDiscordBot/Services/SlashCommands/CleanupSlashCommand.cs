using System.Text;
using Discord;
using Discord.Interactions;
using NitroxDiscordBot.Configuration;

namespace NitroxDiscordBot.Services.SlashCommands;

[DefaultMemberPermissions(GuildPermission.Administrator)]
[Group("cleanup", "Configures periodic cleanup schedules on channels")]
public class CleanupSlashCommand : InteractionModuleBase
{
    private readonly NitroxBotService bot;
    private readonly ChannelCleanupService cleanupService;

    public CleanupSlashCommand(NitroxBotService bot, ChannelCleanupService cleanupService)
    {
        this.bot = bot;
        this.cleanupService = cleanupService;
    }

    [SlashCommand("create", "Creates a cleanup schedule to periodically remove old messages")]
    public async Task CreateCleanupAsync([ChannelTypes(ChannelType.Text)]IChannel channel)
    {
        // TODO: Persist change to a database
        await RespondAsync("Not supported yet", ephemeral: true);
        // if (cleanupService.Definitions.Any(d => d.ChannelId == channel.Id))
        // {
        //     await RespondAsync($"A cleanup schedule already exists for '{channel.Name}'", ephemeral: true);
        //     return;
        // }
        //
        //
        // await RespondAsync($"Created cleanup schedule for channel '{channel.Name}'", ephemeral: true);
    }

    [SlashCommand("list", "Shows active cleanup schedules")]
    public async Task ListAsync()
    {
        StringBuilder sb = new("Active cleanup schedules:");
        sb.AppendLine();
        foreach (ChannelCleanupConfig.ChannelCleanup definition in cleanupService.Definitions)
        {
            IChannel channel = await bot.GetChannel<IChannel>(definition.ChannelId);
            if (channel == null)
            {
                continue;
            }
            sb.Append("- Channel '")
                .Append(channel.Name)
                .Append('\'')
                .Append(" cleans up messages older than ")
                .Append(definition.MaxAge.TotalDays)
                .AppendLine(" days");
        }
        await RespondAsync(sb.ToString(), ephemeral: true);
    }
}