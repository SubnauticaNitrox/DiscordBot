using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NitroxDiscordBot.Configuration;
using NitroxDiscordBot.Services;

namespace NitroxDiscordBot;

public static class Program
{
    public static async Task Main(string[] args)
    {
        IHostBuilder hostBuilder = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, builder) => builder
                .SetBasePath(EnvironmentUtils.ExecutableDirectory)
                .AddJsonFile("appsettings.json", true, true)
                .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", true, true)
                .AddCommandLine(args))
            .ConfigureLogging(loggers => loggers.ClearProviders().AddConsole())
            .ConfigureServices((context, services) =>
            {
                // Configuration
                IConfiguration config = context.Configuration;
                services.AddOptions<NitroxBotConfig>().Bind(config).ValidateDataAnnotations();
                services.AddOptions<ChannelCleanupConfig>().Bind(config).ValidateDataAnnotations();
                services.AddOptions<MotdConfig>().Bind(config).ValidateDataAnnotations();

                // Services
                services.AddSingleton<NitroxBotService>().AddHostedService(provider => provider.GetRequiredService<NitroxBotService>());
                services.AddHostedService<ChannelCleanupService>();
                services.AddHostedService<MotdService>();
            });
        await hostBuilder
            .Build()
            .RunAsync();
    }
}