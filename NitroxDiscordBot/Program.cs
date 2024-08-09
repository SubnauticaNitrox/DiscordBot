using Microsoft.EntityFrameworkCore;
using NitroxDiscordBot.Configuration;
using NitroxDiscordBot.Db;
using NitroxDiscordBot.Services;

if (Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") is null)
{
#if DEBUG
    Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
#else
    Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Production");
#endif
}

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

IServiceCollection services = builder.Services;
ConfigurationManager config = builder.Configuration;

// Configuration
config.AddJsonFile("appsettings.json", true, true);
if (builder.Environment.IsProduction()) config.AddJsonFile("appsettings.Production.json", true, true);
if (builder.Environment.IsDevelopment()) config.AddJsonFile("appsettings.Development.json", true, true);

// Validation
services.AddOptions<NitroxBotConfig>().Bind(config).ValidateDataAnnotations().ValidateOnStart();

// Services
services.Configure<HostOptions>(options =>
{
    options.ServicesStartConcurrently = true;
    options.ServicesStopConcurrently = true;
});
services.AddLogging(opt => opt.AddSimpleConsole(c => c.TimestampFormat = "HH:mm:ss.fff "));
// Don't use Scoped lifetime for DbContext as the services are singleton, not Scoped/Transient.
services.AddDbContext<BotContext>(contextLifetime: ServiceLifetime.Transient, optionsLifetime: ServiceLifetime.Transient);
services.AddHostedSingleton<NitroxBotService>();
services.AddHostedSingleton<CommandHandlerService>();
services.AddHostedSingleton<AutoResponseService>();
services.AddHostedSingleton<ChannelCleanupService>();

IHost host = builder.Build();
// Ensure database is up-to-date
using (IServiceScope scope = host.Services.CreateScope())
{
    BotContext db = scope.ServiceProvider.GetRequiredService<BotContext>();
    db.Database.Migrate();
}
host.Run();