using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using NitroxDiscordBot.Core;
using NitroxDiscordBot.Db;
using NitroxDiscordBot.Db.Models;
using NitroxDiscordBot.Services.SlashCommands.AutoComplete;
using NitroxDiscordBot.Services.SlashCommands.Preconditions;
using static NitroxDiscordBot.Db.Models.AutoResponse;

namespace NitroxDiscordBot.Services.SlashCommands;

[RequireBotDeveloper(Group = "Permission")]
[RequireUserPermission(GuildPermission.Administrator, Group = "Permission")]
[Group("autoresponse", "Configures automatic responses to user messages")]
public class AutoResponseSlashCommands : NitroxInteractionModule
{
    private readonly NitroxBotService bot;
    private readonly BotContext db;
    private readonly ILogger<AutoResponseSlashCommands> log;

    public AutoResponseSlashCommands(NitroxBotService bot, BotContext db, ILogger<AutoResponseSlashCommands> log)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(bot);
        this.bot = bot;
        this.db = db;
        this.log = log;
    }

    [SlashCommand("create", "Creates an auto response for any or specific user message")]
    public async Task CreateAsync([Summary("name")] string autoResponseName)
    {
        if (string.IsNullOrWhiteSpace(autoResponseName))
        {
            await RespondAsync("Please enter a valid and unique auto response name", ephemeral: true);
            return;
        }
        if (await db.AutoResponses.AnyAsync(ar => ar.Name.ToLower() == autoResponseName.ToLower()))
        {
            await RespondAsync($"An auto response with the name `{autoResponseName}` already exists", ephemeral: true);
            return;
        }
        AutoResponse ar = new()
        {
            Name = autoResponseName
        };
        await db.AutoResponses.AddAsync(ar);
        if (await db.SaveChangesAsync() > 0)
        {
            await RespondAsync(
                $"AutoResponse `{ar.Name}` created. Add filters and responses to make it work: /autoresponse add filter ...", ephemeral: true);
        }
    }

    [SlashCommand("remove", "Removes an auto response")]
    public async Task RemoveAsync(
        [Summary("name")] [Autocomplete<AutoResponseNameAutoComplete>]
        string autoResponseName)
    {
        if (string.IsNullOrWhiteSpace(autoResponseName))
        {
            await RespondAsync("Please provide a valid auto response name to remove", ephemeral: true);
            return;
        }

        AutoResponse arToDelete = await db.AutoResponses
            .Include(ar => ar.Responses)
            .Include(ar => ar.Filters)
            .FirstOrDefaultAsync(ar => ar.Name.ToLower() == autoResponseName.ToLower());
        if (arToDelete != null)
        {
            db.AutoResponses.Remove(arToDelete);
            int deletions = await db.SaveChangesAsync();
            if (deletions > 0)
            {
                await RespondAsync($"Auto response `{arToDelete.Name}` has been removed", ephemeral: true);
                return;
            }
        }

        await RespondAsync($"No auto response with the name `{autoResponseName}` exists. Nothing was removed.",
            ephemeral: true);
    }

    [SlashCommand("list", "Shows active auto responses")]
    public async Task ListAsync()
    {
        StringBuilder sb = new("Active auto responses:");
        sb.AppendLine();
        foreach (AutoResponse definition in db.AutoResponses
                     .Include(r => r.Filters)
                     .Include(r => r.Responses))
        {
            sb.Append("- Name: `").Append(definition.Name).Append('`')
                .AppendLine()
                .AppendLine(" - Filters:");
            foreach (Filter filter in definition.Filters)
            {
                sb.Append("   - ").Append(filter.Type).Append(": ");
                switch (filter.Type)
                {
                    case Filter.Types.Channel:
                        foreach (string channelIdString in filter.Value)
                        {
                            if (ulong.TryParse(channelIdString, out ulong channelId))
                            {
                                ITextChannel channel = await bot.GetChannelAsync<ITextChannel>(channelId);
                                if (channel != null)
                                {
                                    sb.Append(channel.GetMentionOrChannelName()).Append(' ');
                                }
                            }
                        }
                        sb.Remove(sb.Length - 1, 1);
                        break;
                    case Filter.Types.UserJoinTimeSpan:
                        sb.Append(string.Join(", ", filter.Value.WhereTryParse<string, TimeSpan>(TimeSpan.TryParse).Select(t => t.ToPrettyFormat())));
                        break;
                    default:
                        sb.Append(string.Join(", ", filter.Value));
                        break;
                }
                sb.AppendLine();
            }

            sb.AppendLine("  - Responses:");
            foreach (Response response in definition.Responses)
            {
                sb.Append("   - ").Append(response.Type).Append(": ");
                switch (response.Type)
                {
                    case Response.Types.MessageUsers:
                        foreach (IGuildUser user in await bot.GetUsersByIdsAsync(Context.Guild, response.Value.WhereTryParse<string, ulong>(ulong.TryParse).ToArray()))
                        {
                            sb.Append(user.Mention).Append(' ');
                        }
                        sb.Remove(sb.Length - 1, 1);
                        break;
                    case Response.Types.MessageRoles:
                        foreach (SocketRole role in bot.GetRolesByIds(Context.Guild as SocketGuild, response.Value.WhereTryParse<string, ulong>(ulong.TryParse).ToArray()))
                        {
                            sb.Append(role.Mention).Append(' ');
                        }
                        sb.Remove(sb.Length - 1, 1);
                        break;
                    default:
                        sb.Append(string.Join(", ", response.Value));
                        break;
                }
                sb.AppendLine();
            }
        }

        await RespondAsync(sb.ToString(), ephemeral: true, allowedMentions: AllowedMentions.None);
    }

    [Group("add", "Add filters or responses to an auto response")]
    public class AutoResponseAdd : NitroxInteractionModule
    {
        private readonly NitroxBotService bot;
        private readonly BotContext db;

        public AutoResponseAdd(NitroxBotService bot, BotContext db)
        {
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(bot);
            this.bot = bot;
            this.db = db;
        }

        [SlashCommand("filter", "Adds a filter to an existing auto response")]
        public async Task AddFilterAsync(
            [Summary("auto-response-name")] [Autocomplete<AutoResponseNameAutoComplete>]
            string autoResponseName,
            [Summary("type")][Autocomplete<AutoResponseFilterTypesAutoComplete>] string typeName,
            [Summary("value")] string value)
        {
            if (!Enum.TryParse(typeName, true, out Filter.Types type))
            {
                await RespondAsync("Please enter a valid filter type", ephemeral: true);
                return;
            }
            AutoResponse ar =
                await db.AutoResponses.Where(ar => ar.Name == autoResponseName)
                    .FirstOrDefaultAsync();
            if (ar == null)
            {
                await RespondAsync($"An auto response with the name `{autoResponseName}` was not found", ephemeral: true);
                return;
            }
            string[] values = value.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (values is null or { Length: 0 })
            {
                await RespondAsync("Filter value must not be empty", ephemeral: true);
                return;
            }
            // Validate values are compatible with filter type
            switch (type)
            {
                case Filter.Types.Channel when values is [_] && ulong.TryParse(values[0], out ulong channelId):
                    if (await bot.GetChannelAsync<ITextChannel>(channelId) == null)
                    {
                        await RespondAsync($"No text channel was found that has id `{channelId}`", ephemeral: true);
                        return;
                    }
                    break;
                case Filter.Types.UserJoinTimeSpan when values is [_] && TimeSpan.TryParse(values[0], out TimeSpan _):
                    break;
                case Filter.Types.MessageWordOrder:
                    break;
                default:
                    await RespondAsync($"Unsupported value `{value}` for filter type `{type}`", ephemeral: true);
                    return;
            }

            ar.Filters.Add(new()
            {
                Type = type,
                Value = values
            });
            db.AutoResponses.Update(ar);
            if (await db.SaveChangesAsync() > 0)
            {
                await RespondAsync($"Filter `{type}` has been added to AutoResponse `{ar.Name}`", ephemeral: true);
            }
        }

        [SlashCommand("response", "Adds a response to an existing auto response")]
        public async Task AddResponseAsync(
            [Summary("auto-response-name")] [Autocomplete<AutoResponseNameAutoComplete>]
            string autoResponseName,
            [Summary("type")][Autocomplete<AutoResponseResponseTypesAutoComplete>] string typeName,
            [Summary("value")] string value)
        {
            if (!Enum.TryParse(typeName, true, out Response.Types type))
            {
                await RespondAsync("Please enter a valid response type", ephemeral: true);
                return;
            }
            AutoResponse ar =
                await db.AutoResponses.Where(ar => ar.Name == autoResponseName)
                    .FirstOrDefaultAsync();
            if (ar == null)
            {
                await RespondAsync($"An auto response with the name `{autoResponseName}` was not found", ephemeral: true);
                return;
            }
            char[] valueSplitChars = type switch
            {
                Response.Types.MessageRoles or Response.Types.MessageUsers => [',', ' '],
                _ => [',']
            };
            string[] values = value.Split(valueSplitChars, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (values is null or { Length: 0 })
            {
                await RespondAsync("Response value must not be empty", ephemeral: true);
                return;
            }
            // Validate values are compatible with response type
            switch (type)
            {
                case Response.Types.MessageRoles:
                    IEnumerable<SocketRole> roles = bot.GetRolesByIds(Context.Guild as SocketGuild, values.WhereTryParse<string, ulong>(ulong.TryParse).ToArray());
                    string[] missingRoles = values.ExceptBy(roles.Select(u => u.Id.ToString()), s => s).ToArray();
                    if (missingRoles.Any())
                    {
                        await RespondAsync($"The following role ids are missing from this server `{string.Join(", ", missingRoles)}`", ephemeral: true);
                        return;
                    }
                    break;
                case Response.Types.MessageUsers:
                    List<IGuildUser> users = await bot.GetUsersByIdsAsync(Context.Guild,
                        values.WhereTryParse<string, ulong>(ulong.TryParse).ToArray());
                    string[] missingUsers = values.ExceptBy(users.Select(u => u.Id.ToString()), s => s).ToArray();
                    if (missingUsers.Any())
                    {
                        await RespondAsync($"The following user ids are missing from this server `{string.Join(", ", missingUsers)}`", ephemeral: true);
                        return;
                    }
                    break;
                default:
                    await RespondAsync($"Unsupported value `{value}` for filter type `{type}`", ephemeral: true);
                    return;
            }

            ar.Responses.Add(new()
            {
                Type = type,
                Value = values
            });
            db.AutoResponses.Update(ar);
            if (await db.SaveChangesAsync() > 0)
            {
                await RespondAsync($"Response `{type}` has been added to {nameof(AutoResponse)} `{ar.Name}`", ephemeral: true);
            }
        }
    }
}