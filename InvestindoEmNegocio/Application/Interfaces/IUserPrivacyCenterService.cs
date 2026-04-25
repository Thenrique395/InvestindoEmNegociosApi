using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IUserPrivacyCenterService
{
    Task<PrivacySummaryDto> GetPrivacySummaryAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<SecuritySummaryDto> GetSecuritySummaryAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<RevokeSessionsResponse> RevokeOwnSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task DeleteOwnAccountAsync(Guid userId, DeleteOwnAccountRequest request, CancellationToken cancellationToken = default);
}
