using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class InvestmentAllocationTargetRepository(InvestDbContext context) : IInvestmentAllocationTargetRepository
{
    public async Task<InvestmentAllocationTarget?> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.InvestmentAllocationTargets
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(InvestmentAllocationTarget target, CancellationToken cancellationToken = default)
    {
        await context.InvestmentAllocationTargets.AddAsync(target, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
