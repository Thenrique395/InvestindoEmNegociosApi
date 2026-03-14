using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public sealed class UserSubscriptionRepository(InvestDbContext context) : IUserSubscriptionRepository
{
    public Task<UserSubscription?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => context.UserSubscriptions.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    public Task AddAsync(UserSubscription subscription, CancellationToken cancellationToken = default)
        => context.UserSubscriptions.AddAsync(subscription, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);
}
