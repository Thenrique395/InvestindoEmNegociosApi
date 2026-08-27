using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class LoanContractRepository(InvestDbContext context, ICurrentSpaceAccessor currentSpaceAccessor) : ILoanContractRepository
{
    public async Task<List<LoanContract>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var spaceId = currentSpaceAccessor.SpaceId;
        return await context.LoanContracts
            .AsNoTracking()
            .Where(x => x.UserId == userId && (!spaceId.HasValue || x.SpaceId == spaceId.Value))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<LoanContract?> GetByIdAsync(Guid contractId, Guid userId, CancellationToken cancellationToken = default)
    {
        var spaceId = currentSpaceAccessor.SpaceId;
        return await context.LoanContracts
            .FirstOrDefaultAsync(x => x.Id == contractId && x.UserId == userId && (!spaceId.HasValue || x.SpaceId == spaceId.Value), cancellationToken);
    }

    public async Task AddAsync(LoanContract contract, CancellationToken cancellationToken = default)
        => await context.LoanContracts.AddAsync(contract, cancellationToken);

    public void Remove(LoanContract contract)
        => context.LoanContracts.Remove(contract);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await context.SaveChangesAsync(cancellationToken);
}
