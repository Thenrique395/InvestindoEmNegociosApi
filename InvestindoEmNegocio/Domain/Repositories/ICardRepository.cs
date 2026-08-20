using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface ICardRepository
{
    Task<List<Card>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Card?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> NicknameExistsAsync(Guid userId, string nickname, Guid? excludeCardId = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Mesma bandeira e mesmos 4 últimos dígitos — a combinação que o índice único
    /// do banco recusa. Consultada antes do insert para a mensagem dizer o motivo certo.
    /// </summary>
    Task<bool> BrandAndLast4ExistsAsync(Guid userId, int brandId, string last4, Guid? excludeCardId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Card card, CancellationToken cancellationToken = default);
    void Remove(Card card);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
