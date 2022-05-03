using System.ComponentModel.DataAnnotations;

namespace NitroxDiscordBot.Configuration;

public class NitroxBotConfig
{
    /// <summary>
    ///     Token used to connect to Discord API.
    /// </summary>
    [Required]
    [RegularExpression(@"[a-zA-Z0-9\.]{40,}")]
    public string Token { get; set; } = "";
}