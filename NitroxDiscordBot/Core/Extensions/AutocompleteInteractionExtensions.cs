using Discord;

namespace NitroxDiscordBot.Core.Extensions;

public static class AutocompleteInteractionExtensions
{
    public static AutocompleteOption GetOption(this IAutocompleteInteraction interaction, string optionKey)
    {
        foreach (AutocompleteOption option in interaction.Data.Options)
        {
            if (option.Name == optionKey) return option;
        }
        return null;
    }
}