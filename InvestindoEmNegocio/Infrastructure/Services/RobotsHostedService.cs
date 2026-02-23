using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
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
        var robots = scope.ServiceProvider.GetServices<IRobotTask>().ToList();
        if (robots.Count == 0)
        {
            logger.LogWarning("Nenhum robô registrado para execução.");
            return;
        }

        var dbContext = scope.ServiceProvider.GetRequiredService<InvestDbContext>();
        foreach (var robot in robots)
        {
            var startedAt = DateTime.UtcNow;
            var success = false;
            var processed = 0;
            string? error = null;

            try
            {
                logger.LogInformation("Iniciando execução do robô {RobotName}.", robot.Name);
                processed = await robot.RunAsync(cancellationToken);
                success = true;
                logger.LogInformation("Robô {RobotName} finalizado. Itens processados: {Processed}.", robot.Name, processed);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                logger.LogError(ex, "Falha na execução do robô {RobotName}.", robot.Name);
            }

            var finishedAt = DateTime.UtcNow;
            dbContext.RobotExecutionLogs.Add(new RobotExecutionLog(
                robot.Name,
                startedAt,
                finishedAt,
                success,
                processed,
                error));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
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
