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
    bool Success,
    int ProcessedCount,
    RobotExecutionMetricsDto Metrics,
    string? Error);

public sealed record RobotStatusDto(
    string RobotName,
    DateTime? LastStartedAt,
    DateTime? LastFinishedAt,
    bool? LastSuccess,
    int LastProcessedCount,
    RobotExecutionMetricsDto LastMetrics,
    string? LastError);

public sealed record RobotExecutionLogDto(
    Guid Id,
    string RobotName,
    DateTime StartedAt,
    DateTime FinishedAt,
    bool Success,
    int ProcessedCount,
    RobotExecutionMetricsDto Metrics,
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
