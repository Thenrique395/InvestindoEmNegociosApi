using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IUserSessionService
{
    Task<AuthResponse> IssueAsync(User user, Guid spaceId, CancellationToken cancellationToken = default);
    Task<AuthResponse> ReissueAsync(User user, DateTime nowUtc, CancellationToken cancellationToken = default);
    Task<AuthResponse> RotateAsync(User user, RefreshToken currentToken, DateTime nowUtc, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetActiveByRawTokenAsync(string rawRefreshToken, DateTime nowUtc, CancellationToken cancellationToken = default);
    Task RevokeByRawTokenAsync(string rawRefreshToken, CancellationToken cancellationToken = default);
    Task RevokeActiveAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken = default);
}
