namespace InvestindoEmNegocio.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid? UserId { get; private set; }
    public string Action { get; private set; }
    public string Entity { get; private set; }
    public string? EntityId { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? Metadata { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private AuditLog()
    {
        Action = string.Empty;
        Entity = string.Empty;
    }

    public AuditLog(Guid? userId, string action, string entity, string? entityId, string? ipAddress, string? userAgent, string? metadata)
    {
        UserId = userId;
        Action = action;
        Entity = entity;
        EntityId = entityId;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        Metadata = metadata;
    }
}
