using Microsoft.EntityFrameworkCore;
using NitroxDiscordBot.Configuration;
using NitroxDiscordBot.Core;
using NitroxDiscordBot.Db;

EnvironmentManager.SetAndGetDotnetEnvironmentByBuildConfiguration();
WebApplicationBuilder builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
{
    Args = args,
    ApplicationName = "Nitrox Discord Bot",
    EnvironmentName = EnvironmentManager.DotnetEnvironment
});
builder.WebHost.UseKestrelCore();

IServiceCollection services = builder.Services;
ConfigurationManager config = builder.Configuration;

// Configuration
config
    .AddJsonFile("appsettings.json", true, true)
    .AddConditionalJsonFile(builder.Environment.IsProduction(), "appsettings.Production.json", true, true)
    .AddConditionalJsonFile(builder.Environment.IsDevelopment(), "appsettings.Development.json", true, true);
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