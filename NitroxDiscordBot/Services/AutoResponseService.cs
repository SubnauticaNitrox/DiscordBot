using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NitroxDiscordBot.Core;
using NitroxDiscordBot.Db;
using NitroxDiscordBot.Db.Models;
using NitroxDiscordBot.Services.Ntfy;
using static NitroxDiscordBot.Db.Models.AutoResponse;

namespace NitroxDiscordBot.Services;

public class AutoResponseService : DiscordBotHostedService
{
    private readonly IMemoryCache cache;
    private readonly INtfyService ntfy;
    private readonly BotContext db;
    private readonly TaskQueueService taskQueue;

    public AutoResponseService(NitroxBotService bot,
        BotContext db,
        ILogger<AutoResponseService> log,
        TaskQueueService taskQueue,
        INtfyService ntfy,
        IMemoryCache cache) : base(bot, log)
    {
        ArgumentNullException.ThrowIfNull(db);
        this.db = db;
        this.taskQueue = taskQueue;
        this.cache = cache;
        this.ntfy = ntfy;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        Bot.MessageReceived += BotOnMessageReceived;
        if (await ntfy.IsAvailable())
        {
            Log.LogInformation("Ntfy at {0} is available", ntfy.Url);
        }
        else
        {
            Log.LogWarning("Ntfy is not available at {0}", ntfy.Url);
        }
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
        var arDefinitions = await cache.GetOrCreateAsync($"database.{nameof(db.AutoResponses)}",
            static async (entry, autoResponses) =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5);
                return await autoResponses
                    .Select(ar => new { ar.Name, ar.Responses, ar.Filters })
                    .AsSingleQuery()
                    .ToArrayAsync();
            }, db.AutoResponses);
        foreach (var definition in arDefinitions)
        {
            if (!MatchesFilters(definition.Filters, author, message)) continue;

            Log.AutoResponseTriggered(definition.Name, message.GetJumpUrl(), author.Username, author.Id);
            string messageJumpUrl = message.GetJumpUrl();
            string messageContent = message.Content.Limit(100, "...");
            // Always send message to Ntfy as only those subscribed to the topic will receive them.
            await taskQueue.EnqueueAsync(ntfy.SendMessageWithTitleAndUrl(INtfyService.AsTopicName(definition.Name),
                $"{author.Username} (#{author.Id}) said", messageContent, "Open Discord",
                messageJumpUrl).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Log.NtfyError(t.Exception, ntfy.Url.ToString());
                }
            }));
            // Send message to other outputs.
            foreach (Response response in definition.Responses)
            {
                switch (response.Type)
                {
                    case Response.Types.MessageRoles:
                        ArraySegment<ulong> roles = response.Value.OfParsable<ulong>();
                        foreach (SocketGuildUser user in Bot.GetUsersWithAnyRoles(author.Guild, roles))
                        {
                            await NotifyModeratorAsync(user, definition.Name, author, messageJumpUrl, messageContent);
                        }
                        break;
                    case Response.Types.MessageUsers:
                        ArraySegment<ulong> userIds = response.Value.OfParsable<ulong>();
                        foreach (IGuildUser user in await Bot.GetUsersByIdsAsync(author.Guild, userIds))
                        {
                            await NotifyModeratorAsync(user, definition.Name, author, messageJumpUrl, messageContent);
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
                    Regex[] regexes = cache.GetOrCreate(cache.CreateKey("filters-word-order", values), static (entry, data) =>
                    {
                        entry.SlidingExpiration = TimeSpan.FromDays(1);
                        return data.CreateRegexesForAnyWordGroupInOrderInSentence();
                    }, values);

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
        string messageJumpUrl,
        string message)
    {
        await taskQueue.EnqueueAsync(userToNotify
            .SendMessageAsync(
                $"[{nameof(AutoResponse)} {responseName}] {authorToReport.Mention} said {messageJumpUrl}:{Environment.NewLine}{message}")
            .ContinueWith(
                t =>
                {
                    if (t.IsFaulted)
                    {
                        Log.DmReportError(t.Exception,messageJumpUrl, userToNotify.Username);
                    }
                }));
    }
}