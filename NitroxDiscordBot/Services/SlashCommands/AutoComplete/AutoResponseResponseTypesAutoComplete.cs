using Discord;
using Discord.Interactions;
using NitroxDiscordBot.Db.Models;

namespace NitroxDiscordBot.Services.SlashCommands.AutoComplete;

public class AutoResponseResponseTypesAutoComplete : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        try
        {
            AutoResponse.Response.Types[] result = Enum.GetValues<AutoResponse.Response.Types>();
            return Task.FromResult(AutocompletionResult.FromSuccess(result.Select(ar => new AutocompleteResult(ar.ToString(), ar.ToString()))));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AutocompletionResult.FromError(ex));
        }
    }
}