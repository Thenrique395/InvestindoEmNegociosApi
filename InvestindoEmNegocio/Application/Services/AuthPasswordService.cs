using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

using BCryptNet = BCrypt.Net.BCrypt;

public class AuthPasswordService(
    IUserRepository userRepository,
    IPasswordResetService passwordResetService,
    ILogger<AuthPasswordService> logger) : IAuthPasswordService
{
    private readonly ILogger<AuthPasswordService> _logger = logger;

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null) throw new UnauthorizedAccessException("Usuário não encontrado.");

        if (!BCryptNet.Verify(request.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Senha atual inválida.");

        var newHash = BCryptNet.HashPassword(request.NewPassword, AuthServicePolicies.BcryptWorkFactor);
        user.ChangePassword(newHash);
        await userRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Password changed {UserId}", user.Id);
    }

    public Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default) =>
        passwordResetService.ForgotPasswordAsync(request, cancellationToken);

    public Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default) =>
        passwordResetService.ResetPasswordAsync(request, cancellationToken);
}
