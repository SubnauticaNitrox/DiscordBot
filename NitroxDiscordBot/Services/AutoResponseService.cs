using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using NitroxDiscordBot.Configuration;
using NitroxDiscordBot.Core;
using static NitroxDiscordBot.Configuration.AutoResponseConfig;

namespace NitroxDiscordBot.Services;

public class AutoResponseService : DiscordBotHostedService
{
    private readonly IOptionsMonitor<AutoResponseConfig> options;
    private readonly char[] sentenceSplitCharacters = ['.', '!', '?', '"', '`'];

    public AutoResponseService(NitroxBotService bot,
        IOptionsMonitor<AutoResponseConfig> options,
        ILogger<AutoResponseService> log) : base(bot, log)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public IEnumerable<Definition> Definitions => options.CurrentValue.AutoResponseDefinitions;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        Bot.MessageReceived += BotOnMessageReceived;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        Bot.MessageReceived -= BotOnMessageReceived;
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
        foreach (Definition definition in Definitions)
        {
            if (!MatchesFilters(definition.Filters, author, message)) continue;

            foreach (Response response in definition.Responses)
            {
                switch (response.Type)
                {
                    case Response.Types.MessageRoles:
                        ulong[] roles = response.Value
                            .Split(" ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                            .Select(r => ulong.TryParse(r, out ulong roleId) ? roleId : 0).Where(r => r != 0)
                            .ToArray();
                        foreach (SocketGuildUser moderator in Bot.GetUsersWithAnyRoles(author.Guild, roles))
                        {
                            await moderator.SendMessageAsync($"[AutoResponse {definition.Name}] {author.Mention} said the following:{Environment.NewLine}{message.Content}");
                        }
                        break;
                    case Response.Types.MessageUsers:
                        ulong[] userIds = response.Value
                            .Split(" ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                            .Select(r => ulong.TryParse(r, out ulong userId) ? userId : 0).Where(r => r != 0)
                            .ToArray();
                        foreach (SocketGuildUser user in Bot.GetUsersByIds(author.Guild, userIds))
                        {
                            await user.SendMessageAsync($"[AutoResponse {definition.Name}] {author.Mention} said the following:{Environment.NewLine}{message.Content}");
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

    private bool MatchesFilters(Filter[] filters, SocketGuildUser author, SocketUserMessage message)
    {
        foreach (Filter filter in filters)
        {
            switch (filter.Type)
            {
                case Filter.Types.Channel when filter.Value is string valueStr && ulong.TryParse(valueStr, out ulong channelId):
                    if (message.Channel.Id != channelId) return false;
                    break;
                case Filter.Types.UserJoinTimeSpan when filter.Value is string valueStr &&
                                                                              TimeSpan.TryParse(valueStr, out TimeSpan valueTimeSpan):
                    if (DateTimeOffset.UtcNow - author.JoinedAt > valueTimeSpan) return false;
                    break;
                case Filter.Types.MessageWordOrder when filter.Values is [..] values:
                    string[] sentences = message.Content.Split(sentenceSplitCharacters,
                        StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    foreach (string sentence in sentences)
                    {
                        foreach (string value in values)
                        {
                            if (sentence.ContainsWordsInOrder(value)) goto next;
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
}