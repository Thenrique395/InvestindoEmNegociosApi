using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class CardRepository(InvestDbContext context, ICurrentSpaceAccessor currentSpaceAccessor) : ICardRepository
{
    public async Task<List<Card>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var spaceId = currentSpaceAccessor.SpaceId;
        return await context.Cards.AsNoTracking()
            .Where(c => c.UserId == userId && (!spaceId.HasValue || c.SpaceId == spaceId.Value))
            .OrderBy(c => c.Nickname)
            .ToListAsync(cancellationToken);
    }

    public async Task<Card?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var spaceId = currentSpaceAccessor.SpaceId;
        return await context.Cards.FirstOrDefaultAsync(
            c => c.Id == id && c.UserId == userId && (!spaceId.HasValue || c.SpaceId == spaceId.Value),
            cancellationToken);
    }

    public async Task<bool> NicknameExistsAsync(Guid userId, string nickname, Guid? excludeCardId = null, CancellationToken cancellationToken = default)
    {
        var normalizedNickname = nickname.Trim().ToUpperInvariant();
        var spaceId = currentSpaceAccessor.SpaceId;

        return await context.Cards.AsNoTracking()
            .Where(c => c.UserId == userId && (!spaceId.HasValue || c.SpaceId == spaceId.Value))
            .Where(c => !excludeCardId.HasValue || c.Id != excludeCardId.Value)
            .AnyAsync(c => c.Nickname.ToUpper() == normalizedNickname, cancellationToken);
    }

    public async Task<bool> BrandAndLast4ExistsAsync(Guid userId, int brandId, string last4, Guid? excludeCardId = null, CancellationToken cancellationToken = default)
    {
        var normalizedLast4 = (last4 ?? string.Empty).Trim();
        if (normalizedLast4.Length == 0) return false;

        // Sem filtro de espaço: o índice único do banco é (UserId, BrandId, Last4) e não
        // conhece espaço — filtrar aqui deixaria a checagem passar e o insert estourar.
        return await context.Cards.AsNoTracking()
            .Where(c => c.UserId == userId && c.BrandId == brandId && c.Last4 == normalizedLast4)
            .AnyAsync(c => !excludeCardId.HasValue || c.Id != excludeCardId.Value, cancellationToken);
    }

    public async Task AddAsync(Card card, CancellationToken cancellationToken = default)
    {
        await context.Cards.AddAsync(card, cancellationToken);
    }

    public void Remove(Card card)
    {
        context.Cards.Remove(card);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
