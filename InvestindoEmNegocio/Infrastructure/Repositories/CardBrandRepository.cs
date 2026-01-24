using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class CardBrandRepository : ICardBrandRepository
{
    private readonly InvestDbContext _context;

    public CardBrandRepository(InvestDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.CardBrands.AsNoTracking().AnyAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<List<CardBrand>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.CardBrands.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<CardBrand>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.CardBrands.AsNoTracking()
            .OrderBy(b => b.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<CardBrand?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.CardBrands.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task AddAsync(CardBrand brand, CancellationToken cancellationToken = default)
    {
        await _context.CardBrands.AddAsync(brand, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
