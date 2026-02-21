using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class CardRepository(InvestDbContext context) : ICardRepository
{
    public async Task<List<Card>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.Cards.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Nickname)
            .ToListAsync(cancellationToken);
    }

    public async Task<Card?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.Cards.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken);
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
