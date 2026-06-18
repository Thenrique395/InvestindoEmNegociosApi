using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAdminUsersService
{
    Task<IReadOnlyList<UserSummaryResponse>> ListAsync(CancellationToken cancellationToken);
    Task<UserSummaryResponse> UpdateRoleAsync(Guid id, string role, CancellationToken cancellationToken);
    Task<UserSummaryResponse> UpdateStatusAsync(Guid id, bool isActive, Guid currentUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserFeatureAccessResponse>> ListFeaturesAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserFeatureAccessResponse>> SetFeatureOverrideAsync(
        Guid id,
        string featureKey,
        bool isEnabled,
        Guid executorUserId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<UserFeatureAccessResponse>> ClearFeatureOverrideAsync(
        Guid id,
        string featureKey,
        Guid executorUserId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, Guid currentUserId, CancellationToken cancellationToken);
    Task<SubscriptionChangeResponse> GrantTrialAsync(Guid id, GrantTrialRequest request, CancellationToken cancellationToken);
}
