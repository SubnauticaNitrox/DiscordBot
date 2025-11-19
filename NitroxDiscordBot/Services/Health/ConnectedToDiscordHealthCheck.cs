using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace NitroxDiscordBot.Services.Health;

internal sealed class ConnectedToDiscordHealthCheck(NitroxBotService botService) : IHealthCheck
{
    private readonly NitroxBotService botService = botService;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
    {
        if (botService.IsConnected)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Bot is connected to Discord"));
        }
        return Task.FromResult(HealthCheckResult.Unhealthy("Bot lost connection to Discord"));
    }
}