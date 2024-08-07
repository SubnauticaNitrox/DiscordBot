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
    private readonly char[] sentenceSplitCharacters = ['.', '!', '?', '"', '`'];

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

            Log.LogInformation($"{nameof(AutoResponse)} '{definition.Name}' triggered for message {message.GetJumpUrl()} by user '{author.Username}' with user id {author.Id}");
            await foreach (Response response in definition.Responses.ToAsyncEnumerable())
            {
                switch (response.Type)
                {
                    case Response.Types.MessageRoles:
                        ulong[] roles = response.Value
                            .Select(r => ulong.TryParse(r, out ulong roleId) ? roleId : 0).Where(r => r != 0)
                            .ToArray();
                        foreach (SocketGuildUser user in Bot.GetUsersWithAnyRoles(author.Guild, roles))
                        {
                            await NotifyModeratorAsync(user, definition.Name, author, message);
                        }
                        break;
                    case Response.Types.MessageUsers:
                        ulong[] userIds = response.Value
                            .Select(r => ulong.TryParse(r, out ulong userId) ? userId : 0).Where(r => r != 0)
                            .ToArray();
                        foreach (IGuildUser user in await Bot.GetUsersByIdsAsync(author.Guild, userIds))
                        {
                            await NotifyModeratorAsync(user, definition.Name, author, message);
                        }
                        break;
                    default:
                        Log.LogWarning("Unhandled response type '{ResponseType}' with value '{ResponseValue}'", response.Type,
                            response.Value);
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
                case Filter.Types.AnyChannel when filter.Value is [_, ..] && filter.Value.WhereTryParse<string, ulong>(ulong.TryParse).ToArray() is [_, ..] channelIds:
                    if (!channelIds.Contains(message.Channel.Id)) return false;
                    break;
                case Filter.Types.UserJoinAge when filter.Value is [{} value] &&
                                                                     TimeSpan.TryParse(value, out TimeSpan valueTimeSpan):
                    if (DateTimeOffset.UtcNow - author.JoinedAt > valueTimeSpan) return false;
                    break;
                case Filter.Types.MessageWordOrder when filter.Value is [_, ..] values:
                    string[] sentences = message.Content.Split(sentenceSplitCharacters,
                        StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    foreach (string sentence in sentences)
                    {
                        foreach (string value in values)
                        {
                            if (sentence.AsSpan().ContainsWordsInOrder(value)) goto next;
                        }
                    }

                    return false;
                default:
                    Log.LogWarning("Unhandled filter type '{FilterType}' with value '{FilterValue}'", filter.Type,
                        filter.Value);
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
            Log.LogError(ex, $"Tried sending DM report about message '{messageToReport.GetJumpUrl()}' to moderator '{userToNotify.Username}' but failed");
        }
    }
}