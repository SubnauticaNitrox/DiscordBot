using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using NitroxDiscordBot.Core;
using NitroxDiscordBot.Db;
using NitroxDiscordBot.Db.Models;
using static NitroxDiscordBot.Db.Models.AutoResponse;

namespace NitroxDiscordBot.Services;

public class AutoResponseService : DiscordBotHostedService
{
    private readonly BotContext db;

    public AutoResponseService(NitroxBotService bot,
        BotContext db,
        ILogger<AutoResponseService> log) : base(bot, log)
    {
        ArgumentNullException.ThrowIfNull(db);
        this.db = db;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        Bot.MessageReceived += BotOnMessageReceived;
        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        Bot.MessageReceived -= BotOnMessageReceived;
        return Task.CompletedTask;
    }

    private async void BotOnMessageReceived(object sender, SocketMessage rawMessage)
    {
        if (rawMessage is not SocketUserMessage message) return;
        if (message.Author.IsBot) return;
        if (message.Author is not SocketGuildUser author) return;

        await ModerateMessageAsync(author, message);
    }

    private async Task ModerateMessageAsync(SocketGuildUser author, SocketUserMessage message)
    {
        foreach (AutoResponse definition in db.AutoResponses
                     .Include(r => r.Filters)
                     .Include(r => r.Responses))
        {
            if (!MatchesFilters(definition.Filters, author, message)) continue;

            Log.AutoResponseTriggered(definition.Name, message.GetJumpUrl(), author.Username, author.Id);
            await foreach (Response response in definition.Responses.ToAsyncEnumerable())
            {
                switch (response.Type)
                {
                    case Response.Types.MessageRoles:
                        ulong[] roles = response.Value.OfParsable<ulong>().ToArray();
                        foreach (SocketGuildUser user in Bot.GetUsersWithAnyRoles(author.Guild, roles))
                        {
                            await NotifyModeratorAsync(user, definition.Name, author, message);
                        }
                        break;
                    case Response.Types.MessageUsers:
                        ulong[] userIds = response.Value.OfParsable<ulong>().ToArray();
                        foreach (IGuildUser user in await Bot.GetUsersByIdsAsync(author.Guild, userIds))
                        {
                            await NotifyModeratorAsync(user, definition.Name, author, message);
                        }
                        break;
                    default:
                        Log.UnhandledResponseType(response.Type, response.Value);
                        break;
                }
            }
        }
    }

    private bool MatchesFilters(IEnumerable<Filter> filters, SocketGuildUser author, SocketUserMessage message)
    {
        foreach (Filter filter in filters)
        {
            switch (filter.Type)
            {
                case Filter.Types.AnyChannel when filter.Value is [_, ..] && filter.Value.OfParsable<ulong>().ToArray() is [_, ..] channelIds:
                    if (!channelIds.Contains(message.Channel.Id)) return false;
                    break;
                case Filter.Types.UserJoinAge when filter.Value is [{} value] &&
                                                                     TimeSpan.TryParse(value, out TimeSpan valueTimeSpan):
                    if (DateTimeOffset.UtcNow - author.JoinedAt > valueTimeSpan) return false;
                    break;
                case Filter.Types.MessageWordOrder when filter.Value is [_, ..] values:
                    return message.Content.AsSpan().ContainsSentenceWithWordOrderOfAny(values);
                default:
                    Log.UnhandledFilterType(filter.Type, filter.Value);
                    break;
            }

next:
            continue;
        }

        return true;
    }

    private async Task NotifyModeratorAsync(IGuildUser userToNotify, string responseName, SocketGuildUser authorToReport, SocketUserMessage messageToReport)
    {
        try
        {
            await userToNotify.SendMessageAsync(
                $"[{nameof(AutoResponse)} {responseName}] {authorToReport.Mention} said {messageToReport.GetJumpUrl()}:{Environment.NewLine}{messageToReport.Content}");
        }
        catch (Exception ex)
        {
            Log.DmReportError(ex, messageToReport.GetJumpUrl(), userToNotify.Username);
        }
    }
}