﻿
using Cronos;

namespace NitroxDiscordBot.Configuration;

public class ChannelCleanupConfig
{
    public IEnumerable<ChannelCleanup> CleanupDefinitions { get; set; }

    public record ChannelCleanup
    {
        public ulong ChannelId { get; init; }
        public TimeSpan MaxAge { get; init; }

        /// <summary>
        ///     Cron formatted schedule. Default to hourly.
        /// </summary>
        public string Schedule { get; init; } = CronExpression.Parse("0 * * * *").ToString();
    }
}
