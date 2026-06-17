using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public sealed class UserSubscriptionRepository(InvestDbContext context) : IUserSubscriptionRepository
{
    public Task<UserSubscription?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => context.UserSubscriptions.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    public Task<UserSubscription?> GetByExternalSubscriptionIdAsync(string externalSubscriptionId, CancellationToken cancellationToken = default)
        => context.UserSubscriptions.FirstOrDefaultAsync(x => x.ExternalSubscriptionId == externalSubscriptionId, cancellationToken);

    public Task<UserSubscription?> GetByExternalCustomerIdAsync(string externalCustomerId, CancellationToken cancellationToken = default)
        => context.UserSubscriptions.FirstOrDefaultAsync(x => x.ExternalCustomerId == externalCustomerId, cancellationToken);

    public async Task<IReadOnlyList<UserSubscription>> ListDueForExpirationAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
        => await context.UserSubscriptions
            .Where(x => x.Status == UserSubscriptionStatus.Active && !x.AutoRenew && x.RenewsAt <= nowUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<UserSubscription>> ListPastDueExpiredGraceAsync(DateTime graceCutoffUtc, CancellationToken cancellationToken = default)
        => await context.UserSubscriptions
            .Where(x => x.Status == UserSubscriptionStatus.PastDue && x.RenewsAt <= graceCutoffUtc)
            .ToListAsync(cancellationToken);

    public Task AddAsync(UserSubscription subscription, CancellationToken cancellationToken = default)
        => context.UserSubscriptions.AddAsync(subscription, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);
}
