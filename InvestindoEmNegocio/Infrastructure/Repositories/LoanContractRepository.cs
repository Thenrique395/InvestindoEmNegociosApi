using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class LoanContractRepository(InvestDbContext context) : ILoanContractRepository
{
    public async Task<List<LoanContract>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => await context.LoanContracts
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<LoanContract?> GetByIdAsync(Guid contractId, Guid userId, CancellationToken cancellationToken = default)
        => await context.LoanContracts.FirstOrDefaultAsync(x => x.Id == contractId && x.UserId == userId, cancellationToken);

    public async Task AddAsync(LoanContract contract, CancellationToken cancellationToken = default)
        => await context.LoanContracts.AddAsync(contract, cancellationToken);

    public void Remove(LoanContract contract)
        => context.LoanContracts.Remove(contract);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await context.SaveChangesAsync(cancellationToken);
}
