using System.Text;
using Discord;
using Discord.Interactions;
using NitroxDiscordBot.Configuration;

namespace NitroxDiscordBot.Services.SlashCommands;

[DefaultMemberPermissions(GuildPermission.Administrator)]
[Group("autoresponse", "Configures automatic response to user messages")]
public class AutoResponseSlashCommand : InteractionModuleBase
{
    private readonly AutoResponseService autoResponseService;

    public AutoResponseSlashCommand(AutoResponseService autoResponseService)
    {
        this.autoResponseService = autoResponseService;
    }

    [SlashCommand("list", "Shows active auto responses")]
    public async Task ListAsync()
    {
        StringBuilder sb = new("Active auto responses:");
        sb.AppendLine();
        foreach (AutoResponseConfig.Definition definition in autoResponseService.Definitions)
        {
            sb.Append("- Name: `").Append(definition.Name).Append('`')
                .AppendLine()
                .Append(" - Filters: `[")
                .Append(string.Join(", ", definition.Filters.Select(f => f)))
                .AppendLine("]`")
                .Append(" - Responses: `[")
                .Append(string.Join(", ", definition.Responses.Select(r => r)))
                .AppendLine("]`");
        }
        await RespondAsync(sb.ToString(), ephemeral: true);
    }
}