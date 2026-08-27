using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class LoanAmortizationRepository(InvestDbContext context, ICurrentSpaceAccessor currentSpaceAccessor) : ILoanAmortizationRepository
{
    public async Task<LoanAmortization?> GetByIdAsync(Guid amortizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var spaceId = currentSpaceAccessor.SpaceId;
        return await context.LoanAmortizations
            .FirstOrDefaultAsync(x => x.Id == amortizationId && x.UserId == userId && (!spaceId.HasValue || x.SpaceId == spaceId.Value), cancellationToken);
    }

    public async Task<LoanAmortization?> GetByIdempotencyKeyAsync(Guid userId, string idempotencyKey, CancellationToken cancellationToken = default)
        => await context.LoanAmortizations.FirstOrDefaultAsync(x => x.UserId == userId && x.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<List<LoanAmortization>> ListByContractAsync(Guid contractId, Guid userId, CancellationToken cancellationToken = default)
    {
        var spaceId = currentSpaceAccessor.SpaceId;
        return await context.LoanAmortizations
            .AsNoTracking()
            .Where(x => x.ContractId == contractId && x.UserId == userId && (!spaceId.HasValue || x.SpaceId == spaceId.Value))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> MaxScheduleVersionAsync(Guid contractId, Guid userId, CancellationToken cancellationToken = default)
    {
        var versions = context.LoanAmortizations.Where(x => x.ContractId == contractId && x.UserId == userId);
        return await versions.AnyAsync(cancellationToken)
            ? await versions.MaxAsync(x => x.ScheduleVersion, cancellationToken)
            : 1;
    }

    public async Task AddAsync(LoanAmortization amortization, CancellationToken cancellationToken = default)
        => await context.LoanAmortizations.AddAsync(amortization, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await context.SaveChangesAsync(cancellationToken);
}
