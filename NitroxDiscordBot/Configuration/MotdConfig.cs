using System.ComponentModel.DataAnnotations;

namespace NitroxDiscordBot.Configuration;

/// <summary>
///     Message-of-the-day (MOTD) configuration.
/// </summary>
[Obsolete("Use database entity instead of configuration")]
public class MotdConfig
{
    public IEnumerable<ChannelMotd>? ChannelMotds { get; set; }

    public class ChannelMotd
    {
        [Required] public ulong ChannelId { get; set; }

        public IEnumerable<MotdMessage>? Messages { get; set; }
    }

    public record MotdMessage
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Url { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public string Footer { get; set; } = "";
        public string FooterIconUrl { get; set; } = "";
        public string ThumbnailUrl { get; set; } = "";
        public string Color { get; set; } = "#00FFFF";
        public IEnumerable<Field>? Fields { get; set; }

        public record Field
        {
            [Required] public string Name { get; set; } = "";
            [Required] public string Content { get; set; } = "";
            public bool IsInline { get; set; }
        }
    }
}