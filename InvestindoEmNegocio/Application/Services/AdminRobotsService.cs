using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Application.Services;

public sealed class AdminRobotsService(
    IEnumerable<IRobotTask> robotTasks,
    IRobotRunner robotRunner,
    IInvestDbContext dbContext) : IAdminRobotsService
{
    public async Task<RobotMonitorResponseDto> MonitorAsync(int take = 50, CancellationToken cancellationToken = default)
    {
        var maxTake = Math.Clamp(take, 1, 200);
        var recentLogs = await dbContext.RobotExecutionLogs
            .AsNoTracking()
            .OrderByDescending(x => x.StartedAt)
            .Take(maxTake)
            .ToListAsync(cancellationToken);

        var taskNames = robotTasks
            .Select(x => x.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var logName in recentLogs.Select(x => x.RobotName))
            taskNames.Add(logName);

        var status = taskNames
            .OrderBy(x => x)
            .Select(name =>
            {
                var lastRun = recentLogs.FirstOrDefault(x => string.Equals(x.RobotName, name, StringComparison.OrdinalIgnoreCase));
                return new RobotStatusDto(
                    name,
                    lastRun?.StartedAt,
                    lastRun?.FinishedAt,
                    lastRun?.Success,
                    lastRun?.ProcessedCount ?? 0,
                    lastRun?.Error);
            })
            .ToList();

        var logs = recentLogs
            .Select(x => new RobotExecutionLogDto(
                x.Id,
                x.RobotName,
                x.StartedAt,
                x.FinishedAt,
                x.Success,
                x.ProcessedCount,
                x.Error))
            .ToList();

        return new RobotMonitorResponseDto(status, logs);
    }

    public Task<RobotRunResultDto?> RunAsync(string robotName, CancellationToken cancellationToken = default) =>
        robotRunner.RunByNameAsync(robotName, cancellationToken);

    public Task<IReadOnlyList<RobotRunResultDto>> RunAllAsync(CancellationToken cancellationToken = default) =>
        robotRunner.RunAllAsync(cancellationToken);
}
