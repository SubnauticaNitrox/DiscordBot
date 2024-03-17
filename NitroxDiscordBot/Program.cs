using NitroxDiscordBot.Configuration;
using NitroxDiscordBot.Core;
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
services.AddOptions<ChannelCleanupConfig>().Bind(config).ValidateDataAnnotations().ValidateOnStart();
services.AddOptions<MotdConfig>().Bind(config).ValidateDataAnnotations().ValidateOnStart();

// Services
services.AddLogging(opt => opt.AddSimpleConsole(c => c.TimestampFormat = "HH:mm:ss.fff "));
// TODO: Discord integration: services.AddScoped<AuthenticationStateProvider, DiscordAuthenticationStateProvider>();
services.AddHostedSingleton<NitroxBotService>();
services.AddHostedSingleton<CommandHandlerService>();
services.AddHostedSingleton<ChannelCleanupService>();

builder.Build().Run();