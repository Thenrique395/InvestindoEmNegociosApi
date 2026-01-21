using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface ICardRepository
{
    Task<List<Card>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Card?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Card card, CancellationToken cancellationToken = default);
    void Remove(Card card);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
