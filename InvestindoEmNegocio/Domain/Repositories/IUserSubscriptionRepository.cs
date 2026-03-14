using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IUserSubscriptionRepository
{
    Task<UserSubscription?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(UserSubscription subscription, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
