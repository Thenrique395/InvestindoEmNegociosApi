using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Infrastructure.Data;

namespace InvestindoEmNegocio.Infrastructure.Services;

public sealed class RobotRunner(
    IEnumerable<IRobotTask> robotTasks,
    InvestDbContext dbContext,
    ILogger<RobotRunner> logger) : IRobotRunner
{
    public async Task<RobotRunResultDto?> RunByNameAsync(string robotName, CancellationToken cancellationToken = default)
    {
        var normalized = (robotName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        var robot = robotTasks.FirstOrDefault(r => string.Equals(r.Name, normalized, StringComparison.OrdinalIgnoreCase));
        if (robot is null)
            return null;

        return await RunOneAsync(robot, cancellationToken);
    }

    public async Task<IReadOnlyList<RobotRunResultDto>> RunAllAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<RobotRunResultDto>();
        foreach (var robot in robotTasks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await RunOneAsync(robot, cancellationToken));
        }
        return results;
    }

    private async Task<RobotRunResultDto> RunOneAsync(IRobotTask robot, CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        var finishedAt = startedAt;
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
        finally
        {
            finishedAt = DateTime.UtcNow;
            dbContext.RobotExecutionLogs.Add(new RobotExecutionLog(
                robot.Name,
                startedAt,
                finishedAt,
                success,
                processed,
                error));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new RobotRunResultDto(robot.Name, startedAt, finishedAt, success, processed, error);
    }
}
