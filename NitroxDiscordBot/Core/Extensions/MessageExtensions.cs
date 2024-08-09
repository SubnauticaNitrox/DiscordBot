using Discord;

namespace NitroxDiscordBot.Core.Extensions;

public static class MessageExtensions
{
    /// <summary>
    ///     Gets the guild id from the message. Returns 0 if the message is not from an <see cref="ITextChannel" />.
    /// </summary>
    public static ulong GetGuildId(this IMessage message)
    {
        if (message?.Channel is ITextChannel { GuildId: var guildId })
        {
            return guildId;
        }
        return 0;
    }
}