namespace NitroxDiscordBot.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddHostedSingleton<TService>(this IServiceCollection services) where TService : class, IHostedService =>
        services.AddSingleton<TService>().AddHostedService(provider => provider.GetRequiredService<TService>());
}