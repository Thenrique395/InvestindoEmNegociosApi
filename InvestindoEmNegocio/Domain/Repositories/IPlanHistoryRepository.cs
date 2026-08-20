using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IPlanHistoryRepository
{
    Task AddAsync(PlanHistoryEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Eventos do lançamento, do mais antigo para o mais novo.</summary>
    Task<List<PlanHistoryEntry>> ListByPlanAsync(Guid planId, Guid userId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
