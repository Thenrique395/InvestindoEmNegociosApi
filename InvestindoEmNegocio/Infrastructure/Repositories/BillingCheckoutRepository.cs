using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public sealed class BillingCheckoutRepository(InvestDbContext context) : IBillingCheckoutRepository
{
    public Task<BillingCheckout?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.BillingCheckouts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<BillingCheckout?> GetByProviderCheckoutIdAsync(string providerCheckoutId, CancellationToken cancellationToken = default)
        => context.BillingCheckouts.FirstOrDefaultAsync(x => x.ProviderCheckoutId == providerCheckoutId, cancellationToken);

    public Task<BillingCheckout?> GetByProviderSubscriptionIdAsync(string providerSubscriptionId, CancellationToken cancellationToken = default)
        => context.BillingCheckouts.FirstOrDefaultAsync(x => x.ProviderSubscriptionId == providerSubscriptionId, cancellationToken);

    public Task<BillingCheckout?> GetByProviderPaymentIntentIdAsync(string providerPaymentIntentId, CancellationToken cancellationToken = default)
        => context.BillingCheckouts.FirstOrDefaultAsync(x => x.ProviderPaymentIntentId == providerPaymentIntentId, cancellationToken);

    public Task<BillingCheckout?> GetLatestPendingByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => context.BillingCheckouts
            .Where(x => x.UserId == userId && (x.Status == BillingCheckoutStatus.Draft || x.Status == BillingCheckoutStatus.Pending || x.Status == BillingCheckoutStatus.RequiresAction))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(BillingCheckout checkout, CancellationToken cancellationToken = default)
        => context.BillingCheckouts.AddAsync(checkout, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);
}
