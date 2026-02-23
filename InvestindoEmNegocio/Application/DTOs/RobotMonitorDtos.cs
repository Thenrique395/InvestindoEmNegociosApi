namespace InvestindoEmNegocio.Application.DTOs;

public sealed record RobotRunResultDto(
    string RobotName,
    DateTime StartedAt,
    DateTime FinishedAt,
    bool Success,
    int ProcessedCount,
    string? Error);

public sealed record RobotStatusDto(
    string RobotName,
    DateTime? LastStartedAt,
    DateTime? LastFinishedAt,
    bool? LastSuccess,
    int LastProcessedCount,
    string? LastError);

public sealed record RobotExecutionLogDto(
    Guid Id,
    string RobotName,
    DateTime StartedAt,
    DateTime FinishedAt,
    bool Success,
    int ProcessedCount,
    string? Error);

public sealed record RobotMonitorResponseDto(
    IReadOnlyList<RobotStatusDto> Robots,
    IReadOnlyList<RobotExecutionLogDto> RecentRuns);
