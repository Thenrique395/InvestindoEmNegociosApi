using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class SpaceRepository(InvestDbContext context) : ISpaceRepository
{
    public async Task<List<Space>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.Spaces.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.IsDefault)
            .ThenBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Space?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.Spaces.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, cancellationToken);
    }

    public async Task<Space?> GetDefaultByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.Spaces.FirstOrDefaultAsync(s => s.UserId == userId && s.IsDefault, cancellationToken);
    }

    public async Task AddAsync(Space space, CancellationToken cancellationToken = default)
    {
        await context.Spaces.AddAsync(space, cancellationToken);
    }

    public void Remove(Space space)
    {
        context.Spaces.Remove(space);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
