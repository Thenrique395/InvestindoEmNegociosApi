using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface ILoanAmortizationRepository
{
    Task<LoanAmortization?> GetByIdAsync(Guid amortizationId, Guid userId, CancellationToken cancellationToken = default);
    Task<LoanAmortization?> GetByIdempotencyKeyAsync(Guid userId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<List<LoanAmortization>> ListByContractAsync(Guid contractId, Guid userId, CancellationToken cancellationToken = default);
    Task<int> MaxScheduleVersionAsync(Guid contractId, Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(LoanAmortization amortization, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
