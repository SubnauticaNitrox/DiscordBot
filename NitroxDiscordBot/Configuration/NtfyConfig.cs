using System.ComponentModel.DataAnnotations;

namespace NitroxDiscordBot.Configuration;

public class NtfyConfig
{
    [Url]
    public string? Url { get; set; }
}