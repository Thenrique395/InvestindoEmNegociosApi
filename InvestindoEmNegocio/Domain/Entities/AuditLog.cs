namespace InvestindoEmNegocio.Domain.Entities;

public class AuditLog(
    Guid? userId,
    string action,
    string entity,
    string? entityId,
    string? ipAddress,
    string? userAgent,
    string? metadata)
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid? UserId { get; private set; } = userId;
    public string Action { get; private set; } = action;
    public string Entity { get; private set; } = entity;
    public string? EntityId { get; private set; } = entityId;
    public string? IpAddress { get; private set; } = ipAddress;
    public string? UserAgent { get; private set; } = userAgent;
    public string? Metadata { get; private set; } = metadata;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private AuditLog() : this(null, string.Empty, string.Empty, null, null, null, null)
    {
    }
}
