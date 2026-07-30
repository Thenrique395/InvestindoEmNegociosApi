using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class LoanPaymentRepository(InvestDbContext context) : ILoanPaymentRepository
{
    public async Task<LoanPayment?> GetByIdAsync(Guid paymentId, Guid userId, CancellationToken cancellationToken = default)
        => await context.LoanPayments.FirstOrDefaultAsync(x => x.Id == paymentId && x.UserId == userId, cancellationToken);

    public async Task<LoanPayment?> GetByIdempotencyKeyAsync(Guid userId, string idempotencyKey, CancellationToken cancellationToken = default)
        => await context.LoanPayments.FirstOrDefaultAsync(x => x.UserId == userId && x.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<List<LoanPayment>> ListByContractAsync(Guid contractId, Guid userId, CancellationToken cancellationToken = default)
        => await context.LoanPayments
            .AsNoTracking()
            .Where(x => x.ContractId == contractId && x.UserId == userId)
            .OrderByDescending(x => x.PaidAt)
            .ToListAsync(cancellationToken);

    public async Task<List<LoanPayment>> ListByInstallmentAsync(Guid installmentId, Guid userId, CancellationToken cancellationToken = default)
        => await context.LoanPayments
            .AsNoTracking()
            .Where(x => x.InstallmentId == installmentId && x.UserId == userId)
            .OrderByDescending(x => x.PaidAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(LoanPayment payment, CancellationToken cancellationToken = default)
        => await context.LoanPayments.AddAsync(payment, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await context.SaveChangesAsync(cancellationToken);
}
