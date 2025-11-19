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
using static NitroxDiscordBot.Services.SlashCommands.AutoComplete.AutoCompleteConstants;

namespace NitroxDiscordBot.Services.SlashCommands;

[RequireBotDeveloper(Group = "Permission")]
[RequireUserPermission(GuildPermission.Administrator, Group = "Permission")]
[Group("autoresponse", "Configures automatic responses to user messages")]
internal sealed class AutoResponseSlashCommands : InteractionModuleBase
{
    private readonly NitroxBotService bot;
    private readonly BotContext db;

    public AutoResponseSlashCommands(NitroxBotService bot, BotContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(bot);
        this.bot = bot;
        this.db = db;
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
                $"AutoResponse `{ar.Name}` created. Add filters and responses to make it work: /autoresponse add filter ...",
                ephemeral: true);
        }
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
                sb.Append("   - **").Append(filter.Type.GetDescriptionOrName()).Append("**: ");
                switch (filter.Type)
                {
                    case Filter.Types.AnyChannel:
                        foreach (string channelIdString in filter.Value)
                        {
                            if (ulong.TryParse(channelIdString, out ulong channelId))
                            {
                                ITextChannel? channel = await bot.GetChannelAsync<ITextChannel>(channelId);
                                if (channel != null)
                                {
                                    sb.Append(channel.GetMentionOrChannelName()).Append(' ');
                                }
                            }
                        }
                        sb.Remove(sb.Length - 1, 1);
                        break;
                    case Filter.Types.UserJoinAge:
                        sb.Append(string.Join(", ", filter.Value.OfParsable<TimeSpan>()
                            .Select(t => t.ToPrettyFormat())));
                        break;
                    default:
                        if (filter.Value.Length > 0)
                        {
                            sb.Append('`');
                        }
                        sb.Append(string.Join("`, `", filter.Value));
                        if (filter.Value.Length > 0)
                        {
                            sb.Append('`');
                        }
                        break;
                }
                sb.AppendLine();
            }

            sb.AppendLine(" - Responses:");
            foreach (Response response in definition.Responses)
            {
                sb.Append("   - **").Append(response.Type.GetDescriptionOrName()).Append("**: ");
                switch (response.Type)
                {
                    case Response.Types.MessageUsers:
                        foreach (IGuildUser user in await bot.GetUsersByIdsAsync(Context.Guild, response.Value.OfParsable<ulong>()))
                        {
                            sb.Append(user.Mention).Append(' ');
                        }
                        sb.Remove(sb.Length - 1, 1);
                        break;
                    case Response.Types.MessageRoles:
                        foreach (SocketRole role in bot.GetRolesByIds(Context.Guild as SocketGuild, response.Value.OfParsable<ulong>()))
                        {
                            sb.Append(role.Mention).Append(' ');
                        }
                        sb.Remove(sb.Length - 1, 1);
                        break;
                    default:
                        if (response.Value.Length > 0)
                        {
                            sb.Append('`');
                        }
                        sb.Append(string.Join("`, `", response.Value));
                        if (response.Value.Length > 0)
                        {
                            sb.Append('`');
                        }
                        break;
                }
                sb.AppendLine();
            }
        }

        await RespondAsync(sb.ToString(), ephemeral: true, allowedMentions: AllowedMentions.None);
    }

    [RequireUserPermission(GuildPermission.ManageMessages, Group = "Permission")]
    [SlashCommand("subscribe", "Subscribe yourself to get notified when an auto response triggers")]
    public async Task SubscribeAsync(
        [Summary("name")] [Autocomplete<AutoResponseNameAutoComplete>]
        string autoResponseName)
    {
        AutoResponse? ar = await db.AutoResponses
            .AsTracking()
            .Include(a => a.Responses)
            .Where(ar => ar.Name == autoResponseName)
            .FirstOrDefaultAsync();
        if (ar == null)
        {
            await RespondAsync($"No {nameof(AutoResponse)} with the name `{autoResponseName}` was found",
                ephemeral: true,
                allowedMentions: AllowedMentions.None);
            return;
        }
        if (ar.Responses.FirstOrDefault(r =>
                r.Type == Response.Types.MessageUsers && r.Value.Contains(Context.User.Id.ToString())) != null)
        {
            await RespondAsync($"You're already subscribed to {nameof(AutoResponse)} `{autoResponseName}`",
                ephemeral: true,
                allowedMentions: AllowedMentions.None);
            return;
        }
        Response? targetResponse = ar.Responses.FirstOrDefault(r => r.Type == Response.Types.MessageUsers);
        if (targetResponse == null)
        {
            targetResponse = new Response
            {
                Type = Response.Types.MessageUsers,
                Value = [Context.User.Id.ToString()]
            };
            ar.Responses.Add(targetResponse);
        }
        else
        {
            targetResponse.Value = [..targetResponse.Value, Context.User.Id.ToString()];
        }

        db.Update(ar);
        if (await db.SaveChangesAsync() > 0)
        {
            await RespondAsync($"You're now subscribed to {nameof(AutoResponse)} `{autoResponseName}`",
                ephemeral: true, allowedMentions: AllowedMentions.None);
        }
        else
        {
            await RespondAsync($"I failed to subscribe you to {nameof(AutoResponse)} `{autoResponseName}`",
                ephemeral: true, allowedMentions: AllowedMentions.None);
        }
    }

    [RequireUserPermission(GuildPermission.ManageMessages, Group = "Permission")]
    [SlashCommand("unsubscribe", "Unsubscribe yourself from an auto response")]
    public async Task UnsubscribeAsync(
        [Summary("name")] [Autocomplete<AutoResponseNameAutoComplete>]
        string autoResponseName)
    {
        AutoResponse? ar = await db.AutoResponses
            .AsTracking()
            .Include(a => a.Responses)
            .Where(ar => ar.Name == autoResponseName)
            .FirstOrDefaultAsync();
        if (ar == null)
        {
            await RespondAsync($"No {nameof(AutoResponse)} with the name `{autoResponseName}` was found",
                ephemeral: true,
                allowedMentions: AllowedMentions.None);
            return;
        }
        if (ar.Responses.All(
                r => r.Type == Response.Types.MessageUsers && !r.Value.Contains(Context.User.Id.ToString())))
        {
            await RespondAsync($"You already aren't subscribed to {nameof(AutoResponse)} `{autoResponseName}`",
                ephemeral: true,
                allowedMentions: AllowedMentions.None);
            return;
        }
        Response? targetResponse = ar.Responses.FirstOrDefault(r => r.Type == Response.Types.MessageUsers);
        if (targetResponse != null)
        {
            targetResponse.Value = targetResponse.Value.Except([Context.User.Id.ToString()]).ToArray();
            if (targetResponse.Value.Length < 1)
            {
                db.Remove(targetResponse);
            }
            else
            {
                db.Update(ar);
            }
        }

        if (await db.SaveChangesAsync() > 0)
        {
            await RespondAsync($"You've unsubscribed from {nameof(AutoResponse)} `{autoResponseName}`",
                ephemeral: true, allowedMentions: AllowedMentions.None);
        }
        else
        {
            await RespondAsync($"I failed to unsubscribe you from {nameof(AutoResponse)} `{autoResponseName}`",
                ephemeral: true, allowedMentions: AllowedMentions.None);
        }
    }

    [Group("add", "Add filters or responses to an auto response")]
    internal sealed class AutoResponseAdd : InteractionModuleBase
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
            [Summary(OptionKeys.AutoResponseName)] [Autocomplete<AutoResponseNameAutoComplete>]
            string autoResponseName,
            [Summary("type")] [Autocomplete<AutoResponseFilterTypesAutoComplete>]
            string typeName,
            [Summary("value")] string value)
        {
            if (!Enum.TryParse(typeName, true, out Filter.Types type))
            {
                await RespondAsync("Please enter a valid filter type", ephemeral: true);
                return;
            }
            AutoResponse? ar =
                await db.AutoResponses.AsTracking().Where(ar => ar.Name == autoResponseName)
                    .FirstOrDefaultAsync();
            if (ar == null)
            {
                await RespondAsync($"An auto response with the name `{autoResponseName}` was not found",
                    ephemeral: true);
                return;
            }
            Filter filter = new()
            {
                Type = type,
            };
            (string? error, string[] values) = await filter.ValidateAsync(bot, value);
            if (!string.IsNullOrWhiteSpace(error))
            {
                await RespondAsync(error, ephemeral: true, allowedMentions: AllowedMentions.None);
                return;
            }

            filter.Value = values;
            ar.Filters.Add(filter);
            db.AutoResponses.Update(ar);
            if (await db.SaveChangesAsync() > 0)
            {
                await RespondAsync($"Filter `{type}` has been added to AutoResponse `{ar.Name}`", ephemeral: true);
            }
        }

        [SlashCommand("response", "Adds a response to an existing auto response")]
        public async Task AddResponseAsync(
            [Summary(OptionKeys.AutoResponseName)] [Autocomplete<AutoResponseNameAutoComplete>]
            string autoResponseName,
            [Summary("type")] [Autocomplete<AutoResponseResponseTypesAutoComplete>]
            string typeName,
            [Summary("value")] string value)
        {
            if (!Enum.TryParse(typeName, true, out Response.Types type))
            {
                await RespondAsync("Please enter a valid response type", ephemeral: true);
                return;
            }
            AutoResponse? ar =
                await db.AutoResponses
                    .AsTracking()
                    .Where(ar => ar.Name == autoResponseName)
                    .FirstOrDefaultAsync();
            if (ar == null)
            {
                await RespondAsync($"An auto response with the name `{autoResponseName}` was not found",
                    ephemeral: true);
                return;
            }
            Response response = new()
            {
                Type = type
            };
            (string? error, string[] values) = await response.ValidateAsync(bot, Context.Guild, value);
            if (!string.IsNullOrEmpty(error))
            {
                await RespondAsync(error, ephemeral: true, allowedMentions: AllowedMentions.None);
                return;
            }

            response.Value = values;
            ar.Responses.Add(response);
            db.AutoResponses.Update(ar);
            if (await db.SaveChangesAsync() > 0)
            {
                await RespondAsync($"Response `{type}` has been added to {nameof(AutoResponse)} `{ar.Name}`",
                    ephemeral: true);
            }
            else
            {
                await RespondAsync($"Failed to add response `{type}` to {nameof(AutoResponse)} `{ar.Name}`",
                    ephemeral: true);
            }
        }
    }

    [Group("remove", "Removes a filter or response from an auto response")]
    public class AutoResponseRemove : InteractionModuleBase
    {
        private readonly NitroxBotService bot;
        private readonly BotContext db;

        public AutoResponseRemove(NitroxBotService bot, BotContext db)
        {
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(bot);
            this.bot = bot;
            this.db = db;
        }

        [SlashCommand("all", "Removes an auto response")]
        public async Task RemoveAsync(
            [Summary("name")] [Autocomplete<AutoResponseNameAutoComplete>]
            string autoResponseName)
        {
            if (string.IsNullOrWhiteSpace(autoResponseName))
            {
                await RespondAsync("Please provide a valid auto response name to remove", ephemeral: true);
                return;
            }

            AutoResponse? arToDelete = await db.AutoResponses
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

        [SlashCommand("filter", "Removes a filter from an auto response")]
        public async Task UpdateFilterAsync(
            [Summary(OptionKeys.AutoResponseName)] [Autocomplete<AutoResponseNameAutoComplete>]
            string autoResponseName,
            [Summary(OptionKeys.FilterId)] [Autocomplete<AutoResponseExistingFiltersByIdAutoComplete>]
            int filterId)
        {
            AutoResponse? ar =
                await db.AutoResponses
                    .Include(ar => ar.Filters)
                    .AsTracking()
                    .FirstOrDefaultAsync(ar => ar.Name == autoResponseName);
            if (ar == null)
            {
                await RespondAsync($"An auto response with the name `{autoResponseName}` was not found",
                    ephemeral: true);
                return;
            }
            Filter? filter = ar.Filters.FirstOrDefault(f => f.FilterId == filterId);
            if (filter == null)
            {
                await RespondAsync($"Requested filter was not found on auto response `{autoResponseName}`",
                    ephemeral: true);
                return;
            }

            ar.Filters.Remove(filter);
            db.AutoResponses.Update(ar);
            if (await db.SaveChangesAsync() > 0)
            {
                await RespondAsync($"Removed filter `{filter.Type}` from the AutoResponse `{ar.Name}` with value `{string.Join(',', filter.Value)}`", ephemeral: true);
            }
            else
            {
                await RespondAsync($"Failed to remove `{filter.Type}` from AutoResponse `{ar.Name}`", ephemeral: true);
            }
        }
    }

    [Group("update", "Updates existing auto responses")]
    internal sealed class AutoResponseUpdate : InteractionModuleBase
    {
        private readonly NitroxBotService bot;
        private readonly BotContext db;

        public AutoResponseUpdate(NitroxBotService bot, BotContext db)
        {
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(bot);
            this.bot = bot;
            this.db = db;
        }

        [SlashCommand("filter", "Updates a filter of an existing auto response")]
        public async Task UpdateFilterAsync(
            [Summary(OptionKeys.AutoResponseName)] [Autocomplete<AutoResponseNameAutoComplete>]
            string autoResponseName,
            [Summary(OptionKeys.FilterId)] [Autocomplete<AutoResponseExistingFiltersByIdAutoComplete>]
            int filterId,
            [Summary("value")] [Autocomplete<AutoResponseExistingFilterValueAutoComplete>]
            string value)
        {
            AutoResponse? ar =
                await db.AutoResponses
                    .Include(ar => ar.Filters)
                    .AsTracking()
                    .FirstOrDefaultAsync(ar => ar.Name == autoResponseName);
            if (ar == null)
            {
                await RespondAsync($"An auto response with the name `{autoResponseName}` was not found",
                    ephemeral: true);
                return;
            }
            Filter? filter = ar.Filters.FirstOrDefault(f => f.FilterId == filterId);
            if (filter == null)
            {
                await RespondAsync($"Requested filter was not found on auto response `{autoResponseName}`",
                    ephemeral: true);
                return;
            }
            (string? error, string[] values) = await filter.ValidateAsync(bot, value);
            if (!string.IsNullOrWhiteSpace(error))
            {
                await RespondAsync(error, ephemeral: true, allowedMentions: AllowedMentions.None);
                return;
            }

            filter.Value = values;
            db.AutoResponses.Update(ar);
            if (await db.SaveChangesAsync() > 0)
            {
                await RespondAsync(
                    $"Filter `{filter.Type}` has been updated on AutoResponse `{ar.Name}` with value `{value}`",
                    ephemeral: true);
            }
            else
            {
                await RespondAsync($"Failed to update `{filter.Type}` on AutoResponse `{ar.Name}` with value `{value}`",
                    ephemeral: true);
            }
        }
    }
}