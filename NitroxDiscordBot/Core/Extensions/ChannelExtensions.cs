using Discord;

namespace NitroxDiscordBot.Core.Extensions;

public static class ChannelExtensions
{
    public static string GetMentionOrDefault(this IChannel channel, Func<IChannel, string> defaultValueFactory = null)
    {
        if (channel == null)
        {
            return defaultValueFactory?.Invoke(null);
        }
        if (channel is IMentionable mentionable)
        {
            return mentionable.Mention;
        }
        return defaultValueFactory?.Invoke(channel);
    }

    public static string GetMentionOrChannelName(this IChannel channel) =>
        channel.GetMentionOrDefault(c => $"channel '{c?.Name ?? ""}'");
}