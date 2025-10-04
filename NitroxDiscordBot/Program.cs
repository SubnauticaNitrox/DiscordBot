using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NitroxDiscordBot.Configuration;
using NitroxDiscordBot.Db;
using NitroxDiscordBot.Services;
using NitroxDiscordBot.Services.Ntfy;

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
if (builder.Environment.IsProduction())
{
    config.AddJsonFile("appsettings.Production.json", true, true);
}
if (builder.Environment.IsDevelopment())
{
    config.AddJsonFile("appsettings.Development.json", true, true);
}

// Validation
services.AddOptions<NitroxBotConfig>().Bind(config).ValidateDataAnnotations().ValidateOnStart();
services.AddOptions<NtfyConfig>().Bind(config.GetSection("Ntfy")).ValidateDataAnnotations().ValidateOnStart();

// Services
services.Configure<HostOptions>(options =>
    {
        options.ServicesStartConcurrently = true;
        options.ServicesStopConcurrently = true;
    }).AddLogging(opt =>
    {
        opt.AddSimpleConsole(c => c.TimestampFormat = "HH:mm:ss.fff ");
        opt.AddFilter($"{nameof(Microsoft)}.{nameof(Microsoft.EntityFrameworkCore)}", LogLevel.Warning);
    })
    .AddMemoryCache()
    // Don't use Scoped lifetime for DbContext as the services are singleton, not Scoped/Transient.
    .AddDbContext<BotContext>(options =>
    {
        options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        if (builder.Environment.IsDevelopment())
        {
            options.EnableSensitiveDataLogging();
        }
    }, ServiceLifetime.Transient, ServiceLifetime.Transient)
    .AddHttpClient<INtfyService, INtfyService>((client, provider) =>
    {
        try
        {
            return new NtfyService(client, provider.GetRequiredService<IOptions<NtfyConfig>>());
        }
        catch
        {
            return new NopNtfyService();
        }
    }).SetHandlerLifetime(TimeSpan.FromMinutes(5)).Services
    .AddHostedSingleton<NitroxBotService>()
    .AddHostedSingleton<TaskQueueService>()
    .AddHostedSingleton<CommandHandlerService>()
    .AddHostedSingleton<AutoResponseService>()
    .AddHostedSingleton<ChannelCleanupService>();

IHost host = builder.Build();
// Ensure database is up-to-date
using (IServiceScope scope = host.Services.CreateScope())
{
    BotContext db = scope.ServiceProvider.GetRequiredService<BotContext>();
    db.Database.Migrate();
}
host.Run();