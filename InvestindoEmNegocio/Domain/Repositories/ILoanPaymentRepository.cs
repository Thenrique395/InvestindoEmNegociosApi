using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface ILoanPaymentRepository
{
    Task<LoanPayment?> GetByIdAsync(Guid paymentId, Guid userId, CancellationToken cancellationToken = default);
    Task<LoanPayment?> GetByIdempotencyKeyAsync(Guid userId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<List<LoanPayment>> ListByContractAsync(Guid contractId, Guid userId, CancellationToken cancellationToken = default);
    Task<List<LoanPayment>> ListByInstallmentAsync(Guid installmentId, Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(LoanPayment payment, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
