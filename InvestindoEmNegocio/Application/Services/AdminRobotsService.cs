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
        var from24h = DateTime.UtcNow.AddHours(-24);
        var logs24h = await dbContext.RobotExecutionLogs
            .AsNoTracking()
            .Where(x => x.StartedAt >= from24h)
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
                    new RobotExecutionMetricsDto(
                        lastRun?.ProcessedCount ?? 0,
                        lastRun?.EmailsAttempted ?? 0,
                        lastRun?.EmailsSent ?? 0,
                        lastRun?.EmailsFailed ?? 0,
                        lastRun?.ZeroItemsReasonCode),
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
                new RobotExecutionMetricsDto(
                    x.ProcessedCount,
                    x.EmailsAttempted,
                    x.EmailsSent,
                    x.EmailsFailed,
                    x.ZeroItemsReasonCode),
                x.Error))
            .ToList();

        var totalRuns = logs24h.Count;
        var successRuns = logs24h.Count(x => x.Success);
        var failedRuns = totalRuns - successRuns;
        var successRate = totalRuns == 0 ? 0m : Math.Round((decimal)successRuns * 100m / totalRuns, 1);
        var summary24h = new RobotMonitorSummaryDto(
            totalRuns,
            successRuns,
            failedRuns,
            successRate,
            logs24h.Sum(x => x.ProcessedCount),
            logs24h.Sum(x => x.EmailsAttempted),
            logs24h.Sum(x => x.EmailsSent),
            logs24h.Sum(x => x.EmailsFailed));

        return new RobotMonitorResponseDto(summary24h, status, logs);
    }

    public Task<RobotRunResultDto?> RunAsync(string robotName, CancellationToken cancellationToken = default) =>
        robotRunner.RunByNameAsync(robotName, cancellationToken);

    public Task<IReadOnlyList<RobotRunResultDto>> RunAllAsync(CancellationToken cancellationToken = default) =>
        robotRunner.RunAllAsync(cancellationToken);
}
