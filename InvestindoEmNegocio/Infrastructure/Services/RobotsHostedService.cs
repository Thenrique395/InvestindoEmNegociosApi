using InvestindoEmNegocio.Application.Interfaces;
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
        if (!settings.Enabled)
        {
            logger.LogInformation("RobotsHostedService desabilitado por configuração.");
            return;
        }

        if (settings.RunOnStartup)
        {
            await RunAllRobotsOnce(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = ComputeDelay(settings.DailyRunTimeUtc);
            logger.LogInformation("Próxima execução dos robôs em {Delay}.", delay);
            await Task.Delay(delay, stoppingToken);
            await RunAllRobotsOnce(stoppingToken);
        }
    }

    private async Task RunAllRobotsOnce(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IRobotRunner>();
        var results = await runner.RunAllAsync(cancellationToken);
        if (results.Count == 0)
        {
            logger.LogWarning("Nenhum robô registrado para execução.");
        }
        else
        {
            logger.LogInformation("Execução diária de robôs concluída. Robôs executados: {Count}", results.Count);
        }
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
}
