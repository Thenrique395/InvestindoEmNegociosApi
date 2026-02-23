namespace InvestindoEmNegocio.Domain.Entities;

public class RobotExecutionLog
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string RobotName { get; private set; } = string.Empty;
    public DateTime StartedAt { get; private set; } = DateTime.UtcNow;
    public DateTime FinishedAt { get; private set; } = DateTime.UtcNow;
    public bool Success { get; private set; }
    public int ProcessedCount { get; private set; }
    public string? Error { get; private set; }

    private RobotExecutionLog() { }

    public RobotExecutionLog(
        string robotName,
        DateTime startedAt,
        DateTime finishedAt,
        bool success,
        int processedCount,
        string? error = null)
    {
        RobotName = string.IsNullOrWhiteSpace(robotName) ? "unknown" : robotName.Trim();
        StartedAt = startedAt;
        FinishedAt = finishedAt;
        Success = success;
        ProcessedCount = processedCount;
        Error = string.IsNullOrWhiteSpace(error) ? null : error.Trim();
    }
}
