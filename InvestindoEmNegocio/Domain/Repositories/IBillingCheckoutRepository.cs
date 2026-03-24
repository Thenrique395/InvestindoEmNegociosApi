using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IBillingCheckoutRepository
{
    Task<BillingCheckout?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BillingCheckout?> GetByProviderCheckoutIdAsync(string providerCheckoutId, CancellationToken cancellationToken = default);
    Task<BillingCheckout?> GetByProviderSubscriptionIdAsync(string providerSubscriptionId, CancellationToken cancellationToken = default);
    Task<BillingCheckout?> GetByProviderPaymentIntentIdAsync(string providerPaymentIntentId, CancellationToken cancellationToken = default);
    Task<BillingCheckout?> GetLatestPendingByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(BillingCheckout checkout, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
