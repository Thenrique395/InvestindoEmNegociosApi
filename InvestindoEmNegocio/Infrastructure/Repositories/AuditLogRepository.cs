using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly InvestDbContext _context;

    public AuditLogRepository(InvestDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        await _context.Set<AuditLog>().AddAsync(auditLog, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
