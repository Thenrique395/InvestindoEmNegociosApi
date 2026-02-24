using System.Diagnostics;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Services;

public sealed class RobotRunner(
    IEnumerable<IRobotTask> robotTasks,
    InvestDbContext dbContext,
    ILogger<RobotRunner> logger) : IRobotRunner
{
    private const int FailureAlertThreshold = 3;

    public async Task<RobotRunResultDto?> RunByNameAsync(string robotName, Guid? triggeredByUserId = null, CancellationToken cancellationToken = default)
    {
        var robot = ResolveRobot(robotName);
        if (robot is null) return null;

        return await RunOneAsync(robot, triggeredByUserId, cancellationToken);
    }

    public async Task<RobotRunResultDto?> RunSafelyByNameAsync(string robotName, int cooldownMinutes = 10, Guid? triggeredByUserId = null, CancellationToken cancellationToken = default)
    {
        var robot = ResolveRobot(robotName);
        if (robot is null) return null;

        var clampedCooldown = Math.Clamp(cooldownMinutes, 1, 120);
        var from = DateTime.UtcNow.AddMinutes(-clampedCooldown);

        var lastSuccessfulRun = await dbContext.RobotExecutionLogs
            .AsNoTracking()
            .Where(x => x.RobotName == robot.Name && x.Success && !x.WasSkipped && x.StartedAt >= from)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastSuccessfulRun is not null)
        {
            var now = DateTime.UtcNow;
            var skipReason = $"RECENT_SUCCESSFUL_RUN_{clampedCooldown}M";
            var correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
            var hostName = Environment.MachineName;

            var log = new RobotExecutionLog(
                robot.Name,
                now,
                now,
                durationMs: 0,
                correlationId,
                hostName,
                triggeredByUserId,
                success: true,
                processedCount: 0,
                wasSkipped: true,
                skipReason: skipReason,
                zeroItemsReasonCode: "SKIPPED_SAFETY_WINDOW");

            dbContext.RobotExecutionLogs.Add(log);
            await dbContext.SaveChangesAsync(cancellationToken);

            return new RobotRunResultDto(
                robot.Name,
                now,
                now,
                0,
                correlationId,
                hostName,
                triggeredByUserId,
                true,
                0,
                new RobotExecutionMetricsDto(0, 0, 0, 0, "SKIPPED_SAFETY_WINDOW"),
                true,
                skipReason,
                null);
        }

        return await RunOneAsync(robot, triggeredByUserId, cancellationToken);
    }

    public async Task<IReadOnlyList<RobotRunResultDto>> RunAllAsync(Guid? triggeredByUserId = null, CancellationToken cancellationToken = default)
    {
        var results = new List<RobotRunResultDto>();
        foreach (var robot in robotTasks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await RunOneAsync(robot, triggeredByUserId, cancellationToken));
        }
        return results;
    }

    private IRobotTask? ResolveRobot(string robotName)
    {
        var normalized = (robotName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        return robotTasks.FirstOrDefault(r => string.Equals(r.Name, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<RobotRunResultDto> RunOneAsync(IRobotTask robot, Guid? triggeredByUserId, CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        var finishedAt = startedAt;
        var durationMs = 0L;
        var success = false;
        var processed = 0;
        var emailsAttempted = 0;
        var emailsSent = 0;
        var emailsFailed = 0;
        string? zeroItemsReasonCode = null;
        string? error = null;
        var correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        var hostName = Environment.MachineName;

        try
        {
            logger.LogInformation("Iniciando execução do robô {RobotName}. CorrelationId={CorrelationId}", robot.Name, correlationId);
            var execution = await robot.RunAsync(cancellationToken);
            processed = execution.ItemsGenerated;
            emailsAttempted = execution.EmailsAttempted;
            emailsSent = execution.EmailsSent;
            emailsFailed = execution.EmailsFailed;
            zeroItemsReasonCode = execution.ZeroItemsReasonCode;
            success = true;
            logger.LogInformation(
                "Robô {RobotName} finalizado. Itens: {Processed}, E-mails tentados: {EmailsAttempted}, enviados: {EmailsSent}, falhas: {EmailsFailed}. CorrelationId={CorrelationId}",
                robot.Name,
                processed,
                emailsAttempted,
                emailsSent,
                emailsFailed,
                correlationId);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            logger.LogError(ex, "Falha na execução do robô {RobotName}. CorrelationId={CorrelationId}", robot.Name, correlationId);
        }
        finally
        {
            finishedAt = DateTime.UtcNow;
            durationMs = Math.Max(0, (long)(finishedAt - startedAt).TotalMilliseconds);

            dbContext.RobotExecutionLogs.Add(new RobotExecutionLog(
                robot.Name,
                startedAt,
                finishedAt,
                durationMs,
                correlationId,
                hostName,
                triggeredByUserId,
                success,
                processed,
                emailsAttempted,
                emailsSent,
                emailsFailed,
                zeroItemsReasonCode,
                wasSkipped: false,
                skipReason: null,
                error));
            await dbContext.SaveChangesAsync(cancellationToken);

            if (!success)
                await NotifyAdminsOnFailureStreakAsync(robot.Name, error, correlationId, cancellationToken);
        }

        return new RobotRunResultDto(
            robot.Name,
            startedAt,
            finishedAt,
            durationMs,
            correlationId,
            hostName,
            triggeredByUserId,
            success,
            processed,
            new RobotExecutionMetricsDto(
                processed,
                emailsAttempted,
                emailsSent,
                emailsFailed,
                zeroItemsReasonCode),
            WasSkipped: false,
            SkipReason: null,
            error);
    }

    private async Task NotifyAdminsOnFailureStreakAsync(string robotName, string? error, string correlationId, CancellationToken cancellationToken)
    {
        var lastRuns = await dbContext.RobotExecutionLogs
            .AsNoTracking()
            .Where(x => x.RobotName == robotName)
            .OrderByDescending(x => x.StartedAt)
            .Take(FailureAlertThreshold)
            .ToListAsync(cancellationToken);

        if (lastRuns.Count < FailureAlertThreshold || lastRuns.Any(x => x.Success))
            return;

        var streakKey = $"robot-failure-streak:{robotName}:{lastRuns.Min(x => x.StartedAt):O}:{lastRuns.Max(x => x.StartedAt):O}";
        var alreadyNotified = await dbContext.UserNotifications
            .AsNoTracking()
            .AnyAsync(x => x.ReferenceKey.StartsWith(streakKey), cancellationToken);
        if (alreadyNotified)
            return;

        var adminIds = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.IsActive && x.Role == UserRole.Admin)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (adminIds.Count == 0)
            return;

        var title = $"Robô {robotName} com {FailureAlertThreshold} falhas consecutivas";
        var message = $"Falha recorrente detectada. Último erro: {(string.IsNullOrWhiteSpace(error) ? "n/d" : error)}. CorrelationId: {correlationId}";
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var notifications = adminIds.Select(adminId => new UserNotification(
            adminId,
            NotificationKind.Overdue,
            title,
            message,
            $"{streakKey}:{adminId}",
            dueDate: today));

        await dbContext.UserNotifications.AddRangeAsync(notifications, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
