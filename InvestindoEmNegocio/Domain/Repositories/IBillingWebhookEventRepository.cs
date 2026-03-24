using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IBillingWebhookEventRepository
{
    Task<BillingWebhookEvent?> GetByProviderEventIdAsync(string provider, string providerEventId, CancellationToken cancellationToken = default);
    Task AddAsync(BillingWebhookEvent item, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
