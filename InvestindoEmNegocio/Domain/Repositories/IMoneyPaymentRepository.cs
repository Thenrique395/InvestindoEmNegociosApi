using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IMoneyPaymentRepository
{
    Task<List<MoneyPayment>> ListByInstallmentIdAsync(Guid installmentId, CancellationToken cancellationToken = default);
    Task<List<MoneyPayment>> ListByInstallmentIdsAsync(IEnumerable<Guid> installmentIds, CancellationToken cancellationToken = default);
    Task<decimal> SumPaidAmountAsync(Guid installmentId, CancellationToken cancellationToken = default);
    Task AddAsync(MoneyPayment payment, CancellationToken cancellationToken = default);
    void RemoveRange(IEnumerable<MoneyPayment> payments);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
