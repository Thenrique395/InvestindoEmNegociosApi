using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public class AuditService(IAuditLogRepository auditLogRepository) : IAuditService
{
    public async Task LogAsync(Guid? userId, string action, string entity, string? entityId, string? ipAddress, string? userAgent, string? metadata, CancellationToken cancellationToken = default)
    {
        var audit = new AuditLog(userId, action, entity, entityId, ipAddress, userAgent, metadata);
        await auditLogRepository.AddAsync(audit, cancellationToken);
        await auditLogRepository.SaveChangesAsync(cancellationToken);
    }
}
