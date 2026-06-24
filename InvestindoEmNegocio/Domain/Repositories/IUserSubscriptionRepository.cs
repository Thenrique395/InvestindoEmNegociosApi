using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IUserSubscriptionRepository
{
    Task<UserSubscription?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserSubscription?> GetByExternalSubscriptionIdAsync(string externalSubscriptionId, CancellationToken cancellationToken = default);
    Task<UserSubscription?> GetByExternalCustomerIdAsync(string externalCustomerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserSubscription>> ListDueForExpirationAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserSubscription>> ListPastDueExpiredGraceAsync(DateTime graceCutoffUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserSubscription>> ListPastDueApproachingGraceEndAsync(DateTime fromRenewsAtUtc, DateTime toRenewsAtUtc, CancellationToken cancellationToken = default);
    Task AddAsync(UserSubscription subscription, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Descarta os valores em memória da entidade já rastreada e os recarrega do banco —
    /// necessário após um DbUpdateConcurrencyException, já que uma query normal retorna a
    /// mesma instância rastreada (e os valores antigos) por resolução de identidade do EF.
    /// </summary>
    Task ReloadAsync(UserSubscription subscription, CancellationToken cancellationToken = default);
}
