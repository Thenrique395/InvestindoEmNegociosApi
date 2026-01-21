using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
