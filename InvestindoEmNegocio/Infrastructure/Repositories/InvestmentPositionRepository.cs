using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class InvestmentPositionRepository(InvestDbContext context, ICurrentSpaceAccessor currentSpaceAccessor) : IInvestmentPositionRepository
{
    public async Task<List<InvestmentPosition>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var spaceId = currentSpaceAccessor.SpaceId;
        return await context.InvestmentPositions
            .AsNoTracking()
            .Include(x => x.Movements.OrderByDescending(m => m.Date))
            .Where(x => x.UserId == userId && (!spaceId.HasValue || x.SpaceId == spaceId.Value))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<InvestmentPosition?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var spaceId = currentSpaceAccessor.SpaceId;
        return await context.InvestmentPositions
            .Include(x => x.Movements.OrderByDescending(m => m.Date))
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId && (!spaceId.HasValue || x.SpaceId == spaceId.Value), cancellationToken);
    }

    public async Task AddAsync(InvestmentPosition position, CancellationToken cancellationToken = default)
    {
        await context.InvestmentPositions.AddAsync(position, cancellationToken);
    }

    public async Task AddMovementAsync(InvestmentMovement movement, CancellationToken cancellationToken = default)
    {
        await context.InvestmentMovements.AddAsync(movement, cancellationToken);
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
