using Hangfire;

namespace NitroxDiscordBot.Configuration;

public class DiscordChannelCleanupConfig
{
    public IEnumerable<ChannelCleanup> CleanupTasks { get; set; }

    public record ChannelCleanup
    {
        public ulong ChannelId { get; set; }
        public TimeSpan MaxAge { get; set; }

        /// <summary>
        ///     Cron formatted schedule.
        /// </summary>
        public string Schedule { get; set; } = Cron.Hourly();

        public override string ToString()
        {
            return $"{nameof(ChannelId)}: {ChannelId}, {nameof(MaxAge)}: {MaxAge}, {nameof(Schedule)}: {Schedule}";
        }
    }
}