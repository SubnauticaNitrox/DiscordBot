using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using NitroxDiscordBot.Db;
using NitroxDiscordBot.Db.Models;
using static NitroxDiscordBot.Services.SlashCommands.AutoComplete.AutoCompleteConstants;

namespace NitroxDiscordBot.Services.SlashCommands.AutoComplete;

/// <summary>
///     Returns auto complete options with the names of all the filters on an existing <see cref="AutoResponse" />, which is in the database.
/// </summary>
internal class AutoResponseExistingFiltersByIdAutoComplete(
    BotContext db,
    ILogger<AutoResponseExistingFiltersByIdAutoComplete> log)
    : AutocompleteHandler
{

    private readonly BotContext db = db;
    private readonly ILogger<AutoResponseExistingFiltersByIdAutoComplete> log = log;

    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction interaction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        try
        {
            AutocompleteOption? autoResponseNameOption = interaction.GetOption(OptionKeys.AutoResponseName);
            if (autoResponseNameOption == null)
            {
                log.AutoCompleteInvalidState(interaction.Data.CommandName);
                return AutocompletionResult.FromSuccess(); // Internal error, not fault of user
            }
            AutoResponse? autoResponse = await db.AutoResponses
                .Include(ar => ar.Filters)
                .FirstOrDefaultAsync(ar => ar.Name == autoResponseNameOption.Value.ToString());
            if (autoResponse == null)
            {
                return AutocompletionResult.FromError(InteractionCommandError.Unsuccessful, $"No auto response found with the name '{autoResponseNameOption.Value}'");
            }
            if (autoResponse.Filters.Count < 1)
            {
                return AutocompletionResult.FromError(InteractionCommandError.Unsuccessful, $"No filters for auto response '{autoResponse.Name}'");
            }

            return AutocompletionResult.FromSuccess(autoResponse.Filters.Select(f => new AutocompleteResult(f.Type.ToString(), f.FilterId)));
        }
        catch (Exception ex)
        {
            return AutocompletionResult.FromError(ex);
        }
    }
}