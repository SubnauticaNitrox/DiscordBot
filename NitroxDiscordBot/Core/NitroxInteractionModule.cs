using Discord;
using Discord.Interactions;

namespace NitroxDiscordBot.Core;

public abstract class NitroxInteractionModule : InteractionModuleBase
{
    protected async Task<InteractionHandle> RespondWithButtonsHandleAsync(string text = null, params ButtonOptions[] buttons)
    {
        InteractionHandle handle = new();
        ActionRowBuilder rowBuilder = new();
        foreach (ButtonOptions button in buttons)
        {
            rowBuilder.WithButton(button.Label, handle.CreateTrackedCustomId(button.Id), button.Style);
        }
        ComponentBuilder buttonBuilder = new ComponentBuilder().AddRow(rowBuilder);
        await RespondAsync(text,  components: buttonBuilder.Build(), ephemeral: true);
        return handle;
    }

    public record ButtonOptions
    {
        public string Id { get; init; }
        public string Label { get; init; }
        public ButtonStyle Style { get; init; } = ButtonStyle.Primary;
    }
}