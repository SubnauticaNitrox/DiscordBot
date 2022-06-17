using System.ComponentModel.DataAnnotations;

namespace NitroxDiscordBot.Configuration;

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
        public record Field
        {
            [Required] public string Name { get; set; } = "";
            [Required] public string Content { get; set; } = "";
            public bool IsInline { get; set; }
        }

        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Url { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public string Footer { get; set; } = "";
        public string FooterIconUrl { get; set; } = "";
        public string ThumbnailUrl { get; set; } = "";
        public IEnumerable<Field>? Fields { get; set; }
    }
}