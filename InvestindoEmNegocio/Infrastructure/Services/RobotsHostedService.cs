using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Infrastructure.Services;

public sealed class RobotsHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<RobotsOptions> options,
    ILogger<RobotsHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        if (settings.RunOnStartup)
        {
            await RunAllRobotsOnce(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var runtimeSettings = await ResolveRuntimeSettings(stoppingToken);
            if (!runtimeSettings.Enabled)
            {
                logger.LogInformation("Execução automática de robôs está desabilitada em Parâmetros. Nova checagem em 1 minuto.");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                continue;
            }

            var delay = ComputeDelay(runtimeSettings.DailyRunTimeUtc);
            logger.LogInformation("Próxima execução dos robôs em {Delay}.", delay);
            await Task.Delay(delay, stoppingToken);
            await RunAllRobotsOnce(stoppingToken);
        }
    }

    private async Task RunAllRobotsOnce(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IRobotRunner>();
        var results = await runner.RunAllAsync(triggeredByUserId: null, cancellationToken);
        if (results.Count == 0)
        {
            logger.LogWarning("Nenhum robô registrado para execução.");
        }
        else
        {
            logger.LogInformation("Execução diária de robôs concluída. Robôs executados: {Count}", results.Count);
        }
    }

    private async Task<RobotRuntimeSettings> ResolveRuntimeSettings(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRobotSettingsRepository>();
        var dbSettings = await repository.GetOrCreateAsync(cancellationToken);
        return new RobotRuntimeSettings(
            Enabled: dbSettings.Enabled,
            DailyRunTimeUtc: dbSettings.DailyRunTimeUtc);
    }

    private static TimeSpan ComputeDelay(string dailyRunTimeUtc)
    {
        var now = DateTime.UtcNow;
        if (!TimeOnly.TryParse(dailyRunTimeUtc, out var runTime))
        {
            runTime = new TimeOnly(8, 0);
        }

        var next = new DateTime(now.Year, now.Month, now.Day, runTime.Hour, runTime.Minute, runTime.Second, DateTimeKind.Utc);
        if (next <= now)
        {
            next = next.AddDays(1);
        }

        return next - now;
    }

    private sealed record RobotRuntimeSettings(bool Enabled, string DailyRunTimeUtc);
}
