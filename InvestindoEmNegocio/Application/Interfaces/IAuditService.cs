namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAuditService
{
    Task LogAsync(Guid? userId, string action, string entity, string? entityId, string? ipAddress, string? userAgent, string? metadata, CancellationToken cancellationToken = default);
}
