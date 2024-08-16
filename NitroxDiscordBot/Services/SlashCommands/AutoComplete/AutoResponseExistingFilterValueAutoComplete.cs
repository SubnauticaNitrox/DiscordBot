using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using NitroxDiscordBot.Db;
using NitroxDiscordBot.Db.Models;
using static NitroxDiscordBot.Services.SlashCommands.AutoComplete.AutoCompleteConstants;

namespace NitroxDiscordBot.Services.SlashCommands.AutoComplete;

/// <summary>
///     Returns the value of an existing filter within an auto response.
/// </summary>
public class AutoResponseExistingFilterValueAutoComplete : AutocompleteHandler
{

    private readonly BotContext db;
    private readonly ILogger<AutoResponseExistingFilterValueAutoComplete> log;

    public AutoResponseExistingFilterValueAutoComplete(BotContext db, ILogger<AutoResponseExistingFilterValueAutoComplete> log)
    {
        this.db = db;
        this.log = log;
    }

    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction interaction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        try
        {
            AutocompleteOption autoResponseNameOption = interaction.GetOption(OptionKeys.AutoResponseName);
            if (autoResponseNameOption == null)
            {
                log.AutoCompleteInvalidState(interaction.Data.CommandName);
                return AutocompletionResult.FromSuccess(); // Internal error, not fault of user
            }
            AutocompleteOption filterIdOption = interaction.GetOption(OptionKeys.FilterId);
            if (filterIdOption == null)
            {
                log.AutoCompleteInvalidState(interaction.Data.CommandName);
                return AutocompletionResult.FromSuccess(); // Internal error, not fault of user
            }
            AutoResponse autoResponse = await db.AutoResponses
                .Include(ar => ar.Filters)
                .FirstOrDefaultAsync(ar => ar.Name == autoResponseNameOption.Value.ToString());
            if (autoResponse == null)
            {
                return AutocompletionResult.FromError(InteractionCommandError.Exception, $"No auto response found with the name '{autoResponseNameOption.Value}'");
            }
            if (autoResponse.Filters.Count < 1)
            {
                return AutocompletionResult.FromError(InteractionCommandError.Exception, $"No filters for auto response '{autoResponse.Name}'");
            }
            int targetFilterId = 0;
            if (filterIdOption.Value is string or not int)
            {
                int.TryParse(filterIdOption.Value.ToString(), out targetFilterId);
            }
            AutoResponse.Filter filter = autoResponse.Filters.FirstOrDefault(f => f.FilterId == targetFilterId);
            if (filter == null)
            {
                return AutocompletionResult.FromError(InteractionCommandError.Exception, "The requested filter was not found");
            }

            string filterValue = string.Join(',', filter.Value);
            return AutocompletionResult.FromSuccess([new AutocompleteResult(filterValue, filterValue)]);
        }
        catch (Exception ex)
        {
            return AutocompletionResult.FromError(ex);
        }
    }
}