using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class LoanInstallmentRepository(InvestDbContext context, ICurrentSpaceAccessor currentSpaceAccessor) : ILoanInstallmentRepository
{
    /*
     * `LoanInstallment` é a única das entidades de empréstimo SEM coluna `SpaceId` — nasce
     * derivada do contrato e não recebeu a coluna quando o multi-tenancy chegou. Em vez de
     * exigir migration e backfill, o isolamento vem do pai: a parcela pertence à área do
     * contrato dela. `EXISTS` no Postgres, não `JOIN` — não multiplica linha.
     */
    private IQueryable<LoanInstallment> NoEspacoAtivo(IQueryable<LoanInstallment> query)
    {
        var spaceId = currentSpaceAccessor.SpaceId;
        if (!spaceId.HasValue) return query;
        return query.Where(x => context.LoanContracts
            .Any(c => c.Id == x.ContractId && c.SpaceId == spaceId.Value));
    }

    public async Task<List<LoanInstallment>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => await NoEspacoAtivo(context.LoanInstallments.AsNoTracking().Where(x => x.UserId == userId))
            .OrderBy(x => x.DueDate)
            .ThenBy(x => x.InstallmentNo)
            .ToListAsync(cancellationToken);

    public async Task<List<LoanInstallment>> ListByContractAsync(Guid contractId, Guid userId, CancellationToken cancellationToken = default)
        => await NoEspacoAtivo(context.LoanInstallments.AsNoTracking().Where(x => x.ContractId == contractId && x.UserId == userId))
            .OrderBy(x => x.InstallmentNo)
            .ToListAsync(cancellationToken);

    public async Task<LoanInstallment?> GetByIdAsync(Guid installmentId, Guid userId, CancellationToken cancellationToken = default)
        => await NoEspacoAtivo(context.LoanInstallments.Where(x => x.Id == installmentId && x.UserId == userId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddRangeAsync(IEnumerable<LoanInstallment> installments, CancellationToken cancellationToken = default)
        => await context.LoanInstallments.AddRangeAsync(installments, cancellationToken);

    public async Task RemoveByContractAsync(Guid contractId, Guid userId, CancellationToken cancellationToken = default)
    {
        var items = await context.LoanInstallments
            .Where(x => x.ContractId == contractId && x.UserId == userId)
            .ToListAsync(cancellationToken);
        context.LoanInstallments.RemoveRange(items);
    }

    public void RemoveRange(IEnumerable<LoanInstallment> installments)
        => context.LoanInstallments.RemoveRange(installments);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await context.SaveChangesAsync(cancellationToken);
}
