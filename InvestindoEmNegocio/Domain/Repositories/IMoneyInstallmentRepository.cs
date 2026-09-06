using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IMoneyInstallmentRepository
{
    Task<List<MoneyInstallment>> ListByUserAsync(Guid userId, InstallmentStatus? status, DateOnly? from, DateOnly? to, MoneyType? type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mesma consulta, para mais de um status. Existe porque "realizado" não é
    /// um status só: uma parcela antecipada foi paga de verdade, só que antes
    /// do vencimento, e precisa somar junto com as pagas.
    /// </summary>
    Task<List<MoneyInstallment>> ListByUserStatusesAsync(Guid userId, IReadOnlyCollection<InstallmentStatus> statuses, DateOnly? from, DateOnly? to, MoneyType? type, CancellationToken cancellationToken = default);
    Task<List<MoneyInstallment>> ListByPlanAsync(Guid planId, Guid userId, CancellationToken cancellationToken = default, bool track = false);
    Task<decimal> SumCardDebtAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<MoneyInstallment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(MoneyInstallment installment, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<MoneyInstallment> installments, CancellationToken cancellationToken = default);
    void Remove(MoneyInstallment installment);
    void RemoveRange(IEnumerable<MoneyInstallment> installments);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
