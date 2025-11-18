using Discord;

namespace NitroxDiscordBot.Core.Extensions;

public static class ChannelExtensions
{
    extension(IChannel? channel)
    {
        public string GetMentionOrDefault(Func<IChannel?, string>? defaultValueFactory = null)
        {
            return channel switch
            {
                null => defaultValueFactory?.Invoke(null) ?? "Unknown",
                IMentionable mentionable => mentionable.Mention,
                _ => defaultValueFactory?.Invoke(channel) ?? "Unknown"
            };
        }

        public string GetMentionOrChannelName() =>
            channel.GetMentionOrDefault(c => $"channel '{c?.Name ?? ""}'");
    }
}