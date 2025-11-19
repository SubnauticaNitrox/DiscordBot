using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NitroxDiscordBot.Core;
using NitroxDiscordBot.Db;
using NitroxDiscordBot.Db.Models;
using NitroxDiscordBot.Services.Ntfy;
using static NitroxDiscordBot.Db.Models.AutoResponse;

namespace NitroxDiscordBot.Services;

[UsedImplicitly]
internal sealed class AutoResponseService(
    NitroxBotService bot,
    BotContext db,
    ILogger<AutoResponseService> log,
    TaskQueueService taskQueue,
    INtfyService ntfy,
    IMemoryCache cache)
    : DiscordBotHostedService(bot, log)
{
    private readonly IMemoryCache cache = cache;
    private readonly BotContext db = db;
    private readonly INtfyService ntfy = ntfy;
    private readonly TaskQueueService taskQueue = taskQueue;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
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

    private async void BotOnMessageReceived(object? sender, SocketMessage message)
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

        bool isFirstTrigger = true;
        foreach (var definition in arDefinitions)
        {
            if (!MatchesFilters(definition.Filters, author, message)) continue;

            string messageJumpUrl = message.GetJumpUrl();
            string messageContent = message.Content.Limit(100, "...");
            if (isFirstTrigger)
            {
                Log.AutoResponseTriggered(definition.Name, message.GetJumpUrl(), author.Username, author.Id);
                isFirstTrigger = false;
                // Always send message to Ntfy as only those subscribed to the topic will receive them.
                await taskQueue.EnqueueAsync(ntfy.SendMessageAsync(INtfyService.AsTopicName(definition.Name),
                    messageContent, $"{author.Username} (#{author.Id}) said", "Open Discord",
                    messageJumpUrl).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        Log.NtfyError(t.Exception, ntfy.Url?.ToString() ?? "none");
                    }
                }));
            }

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
                    Regex[] regexes = cache.GetOrCreate(cache.CreateKey("filters-word-order", values),
                        static (entry, data) =>
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
                        Log.DmReportError(t.Exception, messageJumpUrl, userToNotify.Username);
                    }
                }));
    }
}