using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class UserFeatureOverrideRepository(InvestDbContext context) : IUserFeatureOverrideRepository
{
    public async Task<IReadOnlyList<UserFeatureOverride>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.UserFeatureOverrides
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.FeatureKey)
            .ToListAsync(cancellationToken);
    }

    public async Task<UserFeatureOverride?> GetByUserAndFeatureAsync(Guid userId, string featureKey, CancellationToken cancellationToken = default)
    {
        return await context.UserFeatureOverrides
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.FeatureKey == featureKey,
                cancellationToken);
    }

    public async Task AddAsync(UserFeatureOverride overrideItem, CancellationToken cancellationToken = default)
    {
        await context.UserFeatureOverrides.AddAsync(overrideItem, cancellationToken);
    }

    public void Remove(UserFeatureOverride overrideItem)
    {
        context.UserFeatureOverrides.Remove(overrideItem);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
