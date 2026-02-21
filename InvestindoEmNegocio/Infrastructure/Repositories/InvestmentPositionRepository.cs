using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class InvestmentPositionRepository(InvestDbContext context) : IInvestmentPositionRepository
{
    public async Task<List<InvestmentPosition>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.InvestmentPositions
            .AsNoTracking()
            .Include(x => x.Movements.OrderByDescending(m => m.Date))
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<InvestmentPosition?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.InvestmentPositions
            .Include(x => x.Movements.OrderByDescending(m => m.Date))
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(InvestmentPosition position, CancellationToken cancellationToken = default)
    {
        await context.InvestmentPositions.AddAsync(position, cancellationToken);
    }

    public void Remove(InvestmentPosition position)
    {
        context.InvestmentPositions.Remove(position);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
