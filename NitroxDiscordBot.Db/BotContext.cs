using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NitroxDiscordBot.Db.Models;

namespace NitroxDiscordBot.Db;

public class BotContext : DbContext
{
    public DbSet<Cleanup> Cleanups { get; set; }
    public DbSet<AutoResponse> AutoResponses { get; set; }
    public string DbName { get; init; }

    public BotContext() : this(new DbContextOptions<BotContext>())
    {
    }

    public BotContext(DbContextOptions<BotContext> options) : base(options)
    {
        DbName = "nitroxbot.db";
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        SqliteConnectionStringBuilder builder = new();
        string parentDirectory = AppDomain.CurrentDomain.GetData("DataDirectory") as string
                         ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        Directory.CreateDirectory(parentDirectory);
        builder.DataSource = Path.GetFullPath(Path.Combine(parentDirectory, DbName));
        options.UseSqlite(builder.ToString());
    }
}