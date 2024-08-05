using Discord;

namespace NitroxDiscordBot.Core;

public static class DiscordConstants
{
    /// <summary>
    ///     Used as anchor point for fetching early messages from a Discord channel.
    /// </summary>
    public const ulong EarliestSnowflakeId = 5000000;

    public static readonly AllowedMentions NoMentions = new()
    {
        AllowedTypes = AllowedMentionTypes.None
    };
}