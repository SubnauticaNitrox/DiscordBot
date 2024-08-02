using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using NitroxDiscordBot.Db;

namespace NitroxDiscordBot.Services.SlashCommands.AutoComplete;

public class AutoResponseNameAutoComplete : AutocompleteHandler
{
    private readonly BotContext db;

    public AutoResponseNameAutoComplete(BotContext db)
    {
        this.db = db;
    }

    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        try
        {
            string[] result = await db.AutoResponses.Select(ar => ar.Name)
                .Take(25)
                .ToArrayAsync();
            return AutocompletionResult.FromSuccess(result.Select(ar => new AutocompleteResult(ar, ar)));
        }
        catch (Exception ex)
        {
            return AutocompletionResult.FromError(ex);
        }
    }
}