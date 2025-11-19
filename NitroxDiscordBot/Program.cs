using NitroxDiscordBot.Configuration;
using NitroxDiscordBot.Core;

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

// Configure options
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

// Configure services
services
    .AddRoutingCore()
    .AddMemoryCache()
    .AddAppLogging()
    .AddAppDatabase(builder.Environment.IsDevelopment())
    .AddAppHttp()
    .AddAppHealthChecks()
    .AddAppDomainServices();

// Initialize and run
WebApplication host = builder.Build();
host.Services.UpgradeAppDatabase();
host.MapHealthChecks("/health");
host.UseRouting();
host.Run();