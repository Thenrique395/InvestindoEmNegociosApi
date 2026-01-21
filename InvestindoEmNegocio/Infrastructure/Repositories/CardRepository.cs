using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class CardRepository : ICardRepository
{
    private readonly InvestDbContext _context;

    public CardRepository(InvestDbContext context)
    {
        _context = context;
    }

    public async Task<List<Card>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Cards.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Nickname)
            .ToListAsync(cancellationToken);
    }

    public async Task<Card?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Cards.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(Card card, CancellationToken cancellationToken = default)
    {
        await _context.Cards.AddAsync(card, cancellationToken);
    }

    public void Remove(Card card)
    {
        _context.Cards.Remove(card);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
