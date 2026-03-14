using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class LoanInstallmentRepository(InvestDbContext context) : ILoanInstallmentRepository
{
    public async Task<List<LoanInstallment>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => await context.LoanInstallments
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.DueDate)
            .ThenBy(x => x.InstallmentNo)
            .ToListAsync(cancellationToken);

    public async Task<List<LoanInstallment>> ListByContractAsync(Guid contractId, Guid userId, CancellationToken cancellationToken = default)
        => await context.LoanInstallments
            .AsNoTracking()
            .Where(x => x.ContractId == contractId && x.UserId == userId)
            .OrderBy(x => x.InstallmentNo)
            .ToListAsync(cancellationToken);

    public async Task AddRangeAsync(IEnumerable<LoanInstallment> installments, CancellationToken cancellationToken = default)
        => await context.LoanInstallments.AddRangeAsync(installments, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await context.SaveChangesAsync(cancellationToken);
}
