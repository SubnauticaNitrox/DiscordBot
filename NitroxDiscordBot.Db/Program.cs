using Microsoft.EntityFrameworkCore;
using NitroxDiscordBot.Db;

await using var db = new BotContext();
await db.Database.EnsureDeletedAsync();
db.Database.Migrate();