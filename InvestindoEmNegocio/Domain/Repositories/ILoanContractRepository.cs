using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface ILoanContractRepository
{
    Task<List<LoanContract>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<LoanContract?> GetByIdAsync(Guid contractId, Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(LoanContract contract, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
