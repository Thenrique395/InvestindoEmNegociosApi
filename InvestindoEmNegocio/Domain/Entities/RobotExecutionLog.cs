namespace InvestindoEmNegocio.Domain.Entities;

public class RobotExecutionLog
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string RobotName { get; private set; } = string.Empty;
    public DateTime StartedAt { get; private set; } = DateTime.UtcNow;
    public DateTime FinishedAt { get; private set; } = DateTime.UtcNow;
    public bool Success { get; private set; }
    public int ProcessedCount { get; private set; }
    public int EmailsAttempted { get; private set; }
    public int EmailsSent { get; private set; }
    public int EmailsFailed { get; private set; }
    public string? ZeroItemsReasonCode { get; private set; }
    public string? Error { get; private set; }

    private RobotExecutionLog() { }

    public RobotExecutionLog(
        string robotName,
        DateTime startedAt,
        DateTime finishedAt,
        bool success,
        int processedCount,
        int emailsAttempted = 0,
        int emailsSent = 0,
        int emailsFailed = 0,
        string? zeroItemsReasonCode = null,
        string? error = null)
    {
        RobotName = string.IsNullOrWhiteSpace(robotName) ? "unknown" : robotName.Trim();
        StartedAt = startedAt;
        FinishedAt = finishedAt;
        Success = success;
        ProcessedCount = processedCount;
        EmailsAttempted = Math.Max(0, emailsAttempted);
        EmailsSent = Math.Max(0, emailsSent);
        EmailsFailed = Math.Max(0, emailsFailed);
        ZeroItemsReasonCode = string.IsNullOrWhiteSpace(zeroItemsReasonCode) ? null : zeroItemsReasonCode.Trim();
        Error = string.IsNullOrWhiteSpace(error) ? null : error.Trim();
    }
}
