using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAuthPasswordApplicationService
{
    Task ForgotPasswordAsync(ForgotPasswordRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);
}
