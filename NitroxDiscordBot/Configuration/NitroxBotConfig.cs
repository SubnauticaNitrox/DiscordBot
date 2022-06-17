using System.ComponentModel.DataAnnotations;

namespace NitroxDiscordBot.Configuration;

public class NitroxBotConfig
{
    /// <summary>
    ///     Token used to connect to Discord API.
    /// </summary>
    [Required]
    [RegularExpression(@"[a-zA-Z0-9\._]{58,}")]
    public string Token { get; set; } = "";
}