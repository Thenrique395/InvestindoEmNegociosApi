namespace InvestindoEmNegocio.Application.DTOs;

public sealed record RobotExecutionMetricsDto(
    int ItemsGenerated,
    int EmailsAttempted,
    int EmailsSent,
    int EmailsFailed,
    string? ZeroItemsReasonCode);

public sealed record RobotRunResultDto(
    string RobotName,
    DateTime StartedAt,
    DateTime FinishedAt,
    long DurationMs,
    string CorrelationId,
    string HostName,
    Guid? TriggeredByUserId,
    bool Success,
    int ProcessedCount,
    RobotExecutionMetricsDto Metrics,
    bool WasSkipped,
    string? SkipReason,
    string? Error);

public sealed record RobotStatusDto(
    string RobotName,
    DateTime? LastStartedAt,
    DateTime? LastFinishedAt,
    long LastDurationMs,
    bool? LastSuccess,
    int LastProcessedCount,
    RobotExecutionMetricsDto LastMetrics,
    string? LastCorrelationId,
    string? LastHostName,
    string? LastError);

public sealed record RobotExecutionLogDto(
    Guid Id,
    string RobotName,
    DateTime StartedAt,
    DateTime FinishedAt,
    long DurationMs,
    string CorrelationId,
    string HostName,
    Guid? TriggeredByUserId,
    bool Success,
    int ProcessedCount,
    RobotExecutionMetricsDto Metrics,
    bool WasSkipped,
    string? SkipReason,
    string? Error);

public sealed record RobotMonitorSummaryDto(
    int TotalRuns,
    int SuccessRuns,
    int FailedRuns,
    decimal SuccessRatePercent,
    int ItemsGenerated,
    int EmailsAttempted,
    int EmailsSent,
    int EmailsFailed);

public sealed record RobotMonitorResponseDto(
    RobotMonitorSummaryDto Summary24h,
    IReadOnlyList<RobotStatusDto> Robots,
    IReadOnlyList<RobotExecutionLogDto> RecentRuns);

public sealed record RobotMonitorQueryDto(
    int Take = 50,
    string? RobotName = null,
    bool? Success = null,
    DateTime? From = null,
    DateTime? To = null,
    string? Search = null);
