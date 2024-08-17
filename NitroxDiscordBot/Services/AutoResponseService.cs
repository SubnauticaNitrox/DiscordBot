using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using NitroxDiscordBot.Core;
using NitroxDiscordBot.Db;
using NitroxDiscordBot.Db.Models;
using ZiggyCreatures.Caching.Fusion;
using static NitroxDiscordBot.Db.Models.AutoResponse;

namespace NitroxDiscordBot.Services;

public class AutoResponseService : DiscordBotHostedService
{
    private readonly IFusionCache cache;
    private readonly BotContext db;
    private readonly ConcurrentDictionary<EquatableArray<string>, Regex[]> regexByFilterValue = [];

    public AutoResponseService(NitroxBotService bot,
        BotContext db,
        ILogger<AutoResponseService> log,
        IFusionCache cache) : base(bot, log)
    {
        ArgumentNullException.ThrowIfNull(db);
        this.db = db;
        this.cache = cache;
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

    private async void BotOnMessageReceived(object sender, SocketMessage message)
    {
        if (message is not { Author: SocketGuildUser { IsBot: false } author })
        {
            return;
        }

        await ModerateMessageAsync(author, message);
    }

    private async Task ModerateMessageAsync(SocketGuildUser author, SocketMessage message)
    {
        var arDefinitions = await cache.GetOrSetAsync($"database.{nameof(db.AutoResponses)}", async ct =>
        {
            return await db.AutoResponses
                .Select(ar => new { ar.Name, ar.Responses, ar.Filters })
                .AsSingleQuery()
                .ToArrayAsync(cancellationToken: ct);
        }, options => options.Duration = TimeSpan.FromSeconds(5));
        foreach (var definition in arDefinitions)
        {
            if (!MatchesFilters(definition.Filters, author, message)) continue;

            Log.AutoResponseTriggered(definition.Name, message.GetJumpUrl(), author.Username, author.Id);
            foreach (Response response in definition.Responses)
            {
                switch (response.Type)
                {
                    case Response.Types.MessageRoles:
                        ArraySegment<ulong> roles = response.Value.OfParsable<ulong>();
                        foreach (SocketGuildUser user in Bot.GetUsersWithAnyRoles(author.Guild, roles))
                        {
                            await NotifyModeratorAsync(user, definition.Name, author, message);
                        }
                        break;
                    case Response.Types.MessageUsers:
                        ArraySegment<ulong> userIds = response.Value.OfParsable<ulong>();
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

    private bool MatchesFilters(IEnumerable<Filter> filters, SocketGuildUser author, SocketMessage message)
    {
        foreach (Filter filter in filters)
        {
            switch (filter.Type)
            {
                case Filter.Types.AnyChannel when filter.Value is [_, ..]:
                    if (!filter.Value.ContainsParsable(message.Channel.Id)) return false;
                    break;
                case Filter.Types.UserJoinAge when filter.Value is [{ } value] &&
                                                   TimeSpan.TryParse(value, out TimeSpan valueTimeSpan):
                    if (DateTimeOffset.UtcNow - author.JoinedAt > valueTimeSpan) return false;
                    break;
                case Filter.Types.MessageWordOrder when filter.Value is [_, ..] values:
                    // TODO: Clear cache of unused regexes
                    EquatableArray<string> equatableValues = new(values);
                    if (!regexByFilterValue.TryGetValue(equatableValues, out Regex[] regexes))
                    {
                        regexByFilterValue[equatableValues] = regexes = values.CreateRegexesForAnyWordGroupInOrderInSentence();
                    }
                    if (!regexes.AnyTrue(static (r, content) => r.IsMatch(content), message.Content))
                    {
                        return false;
                    }
                    break;
                default:
                    Log.UnhandledFilterType(filter.Type, filter.Value);
                    break;
            }
        }

        return true;
    }

    private async Task NotifyModeratorAsync(IGuildUser userToNotify,
        string responseName,
        SocketGuildUser authorToReport,
        SocketMessage messageToReport)
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