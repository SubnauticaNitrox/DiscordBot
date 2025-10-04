using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NitroxDiscordBot.Configuration;
using NitroxDiscordBot.Db;
using NitroxDiscordBot.Services;
using NitroxDiscordBot.Services.Health;
using NitroxDiscordBot.Services.Ntfy;

namespace NitroxDiscordBot.Core.Extensions;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHostedSingleton<TService>(this IServiceCollection services)
        where TService : class, IHostedService
    {
        return services.AddSingleton<TService>().AddHostedService(provider => provider.GetRequiredService<TService>());
    }

    public static IServiceCollection AddAppLogging(this IServiceCollection services)
    {
        services.AddLogging(opt =>
        {
            opt.AddSimpleConsole(c => c.TimestampFormat = "HH:mm:ss.fff ");
            opt.AddFilter($"{nameof(Microsoft)}.{nameof(Microsoft.EntityFrameworkCore)}", LogLevel.Warning);
        });
        return services;
    }

    public static IServiceCollection AddAppDatabase(this IServiceCollection services, bool isDevelopment)
    {
        // Don't use Scoped lifetime for DbContext as the services are singleton, not Scoped/Transient.
        services.AddDbContext<BotContext>(options =>
        {
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            if (isDevelopment)
            {
                options.EnableSensitiveDataLogging();
            }
        }, ServiceLifetime.Transient, ServiceLifetime.Transient);
        return services;
    }

    public static IServiceCollection AddAppHttp(this IServiceCollection services)
    {
        return services.AddHealthChecks().Services
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
            }).SetHandlerLifetime(TimeSpan.FromMinutes(5)).Services;
    }

    public static IServiceCollection AddAppHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<ConnectedToDiscordHealthCheck>("Connected");
        return services;
    }

    public static IServiceCollection AddAppDomainServices(this IServiceCollection services)
    {
        services.AddHostedSingleton<NitroxBotService>()
            .AddHostedSingleton<TaskQueueService>()
            .AddHostedSingleton<CommandHandlerService>()
            .AddHostedSingleton<AutoResponseService>()
            .AddHostedSingleton<ChannelCleanupService>();
        return services;
    }
}