using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface ICardsService
{
    Task<IReadOnlyList<CardResponse>> ListAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<CardResponse> CreateAsync(Guid userId, CardRequest request, CancellationToken cancellationToken = default);
    Task<CardResponse?> UpdateAsync(Guid userId, Guid id, CardRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalDebtAsync(Guid userId, CancellationToken cancellationToken = default);
}
