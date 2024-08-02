using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NitroxDiscordBot.Db.Models;

[Table("Cleanups")]
public record Cleanup
{
    public int CleanupId { get; set; }
    [Required]
    public ulong ChannelId { get; set; }
    [Required]
    public TimeSpan AgeThreshold { get; set; } = TimeSpan.FromHours(23);
    [Required]
    [MaxLength(255)]
    public string CronSchedule { get; set; } = "*/5 * * * *";
}