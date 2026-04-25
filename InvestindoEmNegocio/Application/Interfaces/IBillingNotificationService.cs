using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IBillingNotificationService
{
    Task NotifyPendingAsync(User user, BillingCheckout checkout, CancellationToken cancellationToken = default);
    Task NotifyApprovedAsync(Guid userId, BillingCheckout checkout, CancellationToken cancellationToken = default);
    Task NotifyFailedAsync(Guid userId, BillingCheckout checkout, CancellationToken cancellationToken = default, string? customMessage = null);
}
