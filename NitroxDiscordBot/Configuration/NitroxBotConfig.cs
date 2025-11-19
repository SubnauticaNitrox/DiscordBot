using System.ComponentModel.DataAnnotations;
using NitroxDiscordBot.Core;

namespace NitroxDiscordBot.Configuration;

public class NitroxBotConfig
{
    /// <summary>
    ///     Token used to connect to Discord API.
    /// </summary>
    [Required]
    [RegularExpression(@"[a-zA-Z0-9\._]{58,}")]
    public string Token { get; set; } = "";
    [Required]
    [Range(DiscordConstants.EarliestSnowflakeId, ulong.MaxValue)]
    public ulong GuildId { get; set; }
    /// <summary>
    ///     Optional list of Discord user ids that are developers of this bot.
    /// </summary>
    public ulong[] Developers { get; set; } = [];
}