using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.Rest;

namespace NitroxDiscordBot.Services.Commands;

[EnabledInDm(false)]
[DefaultMemberPermissions(GuildPermission.ManageMessages)]
public class InfoCommandModule : ModuleBase<SocketCommandContext>
{
    [Command("ping")]
    [Discord.Commands.Summary("Tests the latency of the bot")]
    public async Task PingAsync()
    {
        DateTimeOffset pingTime = Context.Message.Timestamp;
        RestUserMessage pongMessage = await Context.Channel.SendMessageAsync("Pong!");
        TimeSpan timeDiff = pongMessage.Timestamp - pingTime;
        await pongMessage.ModifyAsync(m => m.Content = $"Pong! `{timeDiff.TotalMilliseconds}ms`");
    }
}
