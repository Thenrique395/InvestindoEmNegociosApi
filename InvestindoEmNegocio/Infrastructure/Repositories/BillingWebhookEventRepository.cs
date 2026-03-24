using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public sealed class BillingWebhookEventRepository(InvestDbContext context) : IBillingWebhookEventRepository
{
    public Task<BillingWebhookEvent?> GetByProviderEventIdAsync(string provider, string providerEventId, CancellationToken cancellationToken = default)
        => context.BillingWebhookEvents.FirstOrDefaultAsync(
            x => x.Provider == provider && x.ProviderEventId == providerEventId,
            cancellationToken);

    public Task AddAsync(BillingWebhookEvent item, CancellationToken cancellationToken = default)
        => context.BillingWebhookEvents.AddAsync(item, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);
}
