using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface ISubscriptionManagementService
{
    Task<SubscriptionChangeResponse> ChangeAsync(Guid userId, ChangeSubscriptionRequest request, CancellationToken cancellationToken = default);
    Task<SubscriptionChangeResponse> CancelAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<SubscriptionChangeResponse> RequestRefundAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<SubscriptionChangeResponse> RequestTrialAsync(Guid userId, CancellationToken cancellationToken = default);
}
