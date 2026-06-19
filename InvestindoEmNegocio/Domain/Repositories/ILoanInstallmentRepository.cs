using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface ILoanInstallmentRepository
{
    Task<List<LoanInstallment>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<LoanInstallment>> ListByContractAsync(Guid contractId, Guid userId, CancellationToken cancellationToken = default);
    Task<LoanInstallment?> GetByIdAsync(Guid installmentId, Guid userId, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<LoanInstallment> installments, CancellationToken cancellationToken = default);
    Task RemoveByContractAsync(Guid contractId, Guid userId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
