using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IUserFeatureOverrideRepository
{
    Task<IReadOnlyList<UserFeatureOverride>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserFeatureOverride?> GetByUserAndFeatureAsync(Guid userId, string featureKey, CancellationToken cancellationToken = default);
    Task AddAsync(UserFeatureOverride overrideItem, CancellationToken cancellationToken = default);
    void Remove(UserFeatureOverride overrideItem);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
