using Discord;
using Discord.Interactions;
using NitroxDiscordBot.Services.SlashCommands.Preconditions;

namespace NitroxDiscordBot.Services.SlashCommands;

[RequireBotDeveloper(Group = "Permission")]
[RequireUserPermission(GuildPermission.Administrator, Group = "Permission")]
[Group("nitroxdiag", "Commands to diagnose the bot")]
public sealed class DiagnosticCommands(NitroxBotService bot, ILogger<DiagnosticCommands> log) : InteractionModuleBase
{
    private readonly NitroxBotService bot = bot;
    private readonly ILogger<DiagnosticCommands> log = log;

    [SlashCommand("ping", "Pings the bot!")]
    public async Task PingAsync()
    {
        await RespondAsync("Pong", allowedMentions: AllowedMentions.None, ephemeral: true);
        await bot.PingAsync(await GetOriginalResponseAsync(), Context.User, ModifyOriginalResponseAsync, Context.Interaction.CreatedAt);
    }
}