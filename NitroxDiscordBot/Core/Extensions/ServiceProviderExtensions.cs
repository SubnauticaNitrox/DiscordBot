using Microsoft.EntityFrameworkCore;
using NitroxDiscordBot.Db;

namespace NitroxDiscordBot.Core.Extensions;

internal static class ServiceProviderExtensions
{
    extension(IServiceProvider provider)
    {
        /// <summary>
        ///     Ensures database is up-to-date.
        /// </summary>
        public void UpgradeAppDatabase()
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BotContext>();
            db.Database.Migrate();
        }
    }
}