using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IMoneyInstallmentRepository
{
    Task<List<MoneyInstallment>> ListByUserAsync(Guid userId, InstallmentStatus? status, DateOnly? from, DateOnly? to, MoneyType? type, CancellationToken cancellationToken = default);
    Task<List<MoneyInstallment>> ListByPlanAsync(Guid planId, Guid userId, CancellationToken cancellationToken = default);
    Task<decimal> SumCardDebtAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<MoneyInstallment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(MoneyInstallment installment, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<MoneyInstallment> installments, CancellationToken cancellationToken = default);
    void Remove(MoneyInstallment installment);
    void RemoveRange(IEnumerable<MoneyInstallment> installments);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
