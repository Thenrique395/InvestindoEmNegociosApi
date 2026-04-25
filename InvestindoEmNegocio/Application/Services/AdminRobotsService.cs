using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Application.Services;

public sealed class AdminRobotsService(
    IEnumerable<IRobotTask> robotTasks,
    IRobotRunner robotRunner,
    IInvestDbContext dbContext) : IAdminRobotMonitorService, IAdminRobotExecutionService
{
    public async Task<RobotMonitorResponseDto> MonitorAsync(RobotMonitorQueryDto query, CancellationToken cancellationToken = default)
    {
        var maxTake = Math.Clamp(query.Take, 1, 200);
        var logsQuery = dbContext.RobotExecutionLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.RobotName))
        {
            var robotName = query.RobotName.Trim();
            logsQuery = logsQuery.Where(x => x.RobotName == robotName);
        }

        if (query.Success.HasValue)
            logsQuery = logsQuery.Where(x => x.Success == query.Success.Value);

        if (query.From.HasValue)
            logsQuery = logsQuery.Where(x => x.StartedAt >= query.From.Value);

        if (query.To.HasValue)
            logsQuery = logsQuery.Where(x => x.StartedAt <= query.To.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            logsQuery = logsQuery.Where(x =>
                x.RobotName.ToLower().Contains(search) ||
                (x.Error != null && x.Error.ToLower().Contains(search)) ||
                (x.SkipReason != null && x.SkipReason.ToLower().Contains(search)));
        }

        var recentLogs = await logsQuery
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
                    lastRun?.DurationMs ?? 0,
                    lastRun?.Success,
                    lastRun?.ProcessedCount ?? 0,
                    new RobotExecutionMetricsDto(
                        lastRun?.ProcessedCount ?? 0,
                        lastRun?.EmailsAttempted ?? 0,
                        lastRun?.EmailsSent ?? 0,
                        lastRun?.EmailsFailed ?? 0,
                        lastRun?.ZeroItemsReasonCode),
                    lastRun?.CorrelationId,
                    lastRun?.HostName,
                    lastRun?.Error);
            })
            .ToList();

        var logs = recentLogs
            .Select(x => new RobotExecutionLogDto(
                x.Id,
                x.RobotName,
                x.StartedAt,
                x.FinishedAt,
                x.DurationMs,
                x.CorrelationId,
                x.HostName,
                x.TriggeredByUserId,
                x.Success,
                x.ProcessedCount,
                new RobotExecutionMetricsDto(
                    x.ProcessedCount,
                    x.EmailsAttempted,
                    x.EmailsSent,
                    x.EmailsFailed,
                    x.ZeroItemsReasonCode),
                x.WasSkipped,
                x.SkipReason,
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

    public Task<RobotRunResultDto?> RunAsync(string robotName, bool force, int cooldownMinutes, Guid? triggeredByUserId = null, CancellationToken cancellationToken = default)
    {
        if (force)
            return robotRunner.RunByNameAsync(robotName, triggeredByUserId, cancellationToken);

        return robotRunner.RunSafelyByNameAsync(robotName, cooldownMinutes, triggeredByUserId, cancellationToken);
    }

    public Task<IReadOnlyList<RobotRunResultDto>> RunAllAsync(Guid? triggeredByUserId = null, CancellationToken cancellationToken = default) =>
        robotRunner.RunAllAsync(triggeredByUserId, cancellationToken);
}
