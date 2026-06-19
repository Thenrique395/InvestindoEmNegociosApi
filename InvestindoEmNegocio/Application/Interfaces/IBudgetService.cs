using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IBudgetService
{
    Task<BudgetResponse> GetAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default);
    Task<BudgetResponse> UpsertItemsAsync(Guid userId, int year, int month, IReadOnlyList<BudgetItemRequest> items, CancellationToken cancellationToken = default);
    Task DeleteItemAsync(Guid userId, Guid itemId, CancellationToken cancellationToken = default);
}
