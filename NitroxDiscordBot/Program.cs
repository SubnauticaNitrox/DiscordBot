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

WebApplicationBuilder builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
{
    Args = args,
    ApplicationName = "Nitrox Discord Bot",
    EnvironmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
});
builder.WebHost.UseKestrelCore();

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
services.AddOptions<NitroxBotConfig>().Bind(config).ValidateDataAnnotations().ValidateOnStart();
services.AddOptions<NtfyConfig>().Bind(config.GetSection("Ntfy")).ValidateDataAnnotations().ValidateOnStart();
services.Configure<HostOptions>(options =>
{
    options.ServicesStartConcurrently = true;
    options.ServicesStopConcurrently = true;
});

// Services
services
    .AddRoutingCore()
    .AddMemoryCache()
    .AddAppLogging()
    .AddAppDatabase(builder.Environment.IsDevelopment())
    .AddAppHttp()
    .AddAppHealthChecks()
    .AddAppDomainServices();

WebApplication host = builder.Build();
// Ensure database is up-to-date
using (IServiceScope scope = host.Services.CreateScope())
{
    BotContext db = scope.ServiceProvider.GetRequiredService<BotContext>();
    db.Database.Migrate();
}

host.MapHealthChecks("/health");
host.UseRouting();
host.Run();