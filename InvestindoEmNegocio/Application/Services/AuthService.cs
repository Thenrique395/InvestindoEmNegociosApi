using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;

namespace InvestindoEmNegocio.Application.Services;

public class AuthService(
    IAuthRegistrationService authRegistrationService,
    IAuthAccessService authAccessService,
    IAuthPasswordService authPasswordService)
    : IAuthService
{
    public Task<AuthResponse> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default) =>
        authRegistrationService.RegisterAsync(request, cancellationToken);

    public Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) =>
        authAccessService.LoginAsync(request, cancellationToken);

    public Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default) =>
        authPasswordService.ForgotPasswordAsync(request, cancellationToken);

    public Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default) =>
        authPasswordService.ResetPasswordAsync(request, cancellationToken);

    public Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default) =>
        authPasswordService.ChangePasswordAsync(userId, request, cancellationToken);

    public Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default) =>
        authAccessService.RefreshAsync(request, cancellationToken);

    public Task LogoutAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default) =>
        authAccessService.LogoutAsync(request, cancellationToken);
}
