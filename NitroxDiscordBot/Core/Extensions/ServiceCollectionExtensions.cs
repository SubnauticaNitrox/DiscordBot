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
    extension(IServiceCollection services)
    {
        public IServiceCollection AddHostedSingleton<TService>()
            where TService : class, IHostedService
        {
            return services.AddSingleton<TService>().AddHostedService(provider => provider.GetRequiredService<TService>());
        }

        public IServiceCollection AddAppLogging()
        {
            services.AddLogging(opt =>
            {
                opt.AddSimpleConsole(c => c.TimestampFormat = "HH:mm:ss.fff ");
                opt.AddFilter($"{nameof(Microsoft)}.{nameof(Microsoft.EntityFrameworkCore)}", LogLevel.Warning);
            });
            return services;
        }

        public IServiceCollection AddAppDatabase(bool isDevelopment)
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

        public IServiceCollection AddAppHttp()
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

        public IServiceCollection AddAppHealthChecks()
        {
            services.AddHealthChecks().AddCheck<ConnectedToDiscordHealthCheck>("Connected");
            return services;
        }

        public IServiceCollection AddAppDomainServices()
        {
            services.AddHostedSingleton<NitroxBotService>()
                .AddHostedSingleton<TaskQueueService>()
                .AddHostedSingleton<CommandHandlerService>()
                .AddHostedSingleton<AutoResponseService>()
                .AddHostedSingleton<ChannelCleanupService>();
            return services;
        }
    }
}