using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Validation;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

using BCryptNet = BCrypt.Net.BCrypt;

public class AuthRegistrationService(
    IUserRepository userRepository,
    IUserAccountBootstrapService userAccountBootstrapService,
    ISpaceBootstrapService spaceBootstrapService,
    IEmailConfirmationService emailConfirmationService,
    ILogger<AuthRegistrationService> logger) : IAuthRegistrationService
{
    private readonly ILogger<AuthRegistrationService> _logger = logger;

    public async Task<RegisteredUserResponse> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = AuthServicePolicies.NormalizeEmail(request.Email);
        if (await userRepository.EmailExistsAsync(normalizedEmail, cancellationToken))
            throw new InvalidOperationException("E-mail já está em uso.");

        var document = CpfValidation.Normalize(request.Document);
        if (await userRepository.DocumentExistsAsync(document, cancellationToken))
            throw new InvalidOperationException("CPF já está em uso.");

        var passwordHash = BCryptNet.HashPassword(request.Password, AuthServicePolicies.BcryptWorkFactor);
        var user = new User(request.Name.Trim(), normalizedEmail, passwordHash, document);

        await userRepository.AddAsync(user, cancellationToken);
        await userRepository.SaveChangesAsync(cancellationToken);
        var spaceId = await spaceBootstrapService.EnsureDefaultSpaceAsync(user, cancellationToken);
        await userAccountBootstrapService.EnsureDefaultAccountForBasicAsync(user, spaceId, cancellationToken);

        // Double opt-in: NÃO loga o usuário. Envia o e-mail de confirmação; o login só é liberado
        // depois de confirmar (gate em AuthAccessService.LoginAsync).
        await emailConfirmationService.SendConfirmationAsync(user, cancellationToken);

        _logger.LogInformation("User registered {UserId} (pending email confirmation)", user.Id);
        return new RegisteredUserResponse(user.Id, user.Name, user.Email, RequiresEmailConfirmation: true);
    }
}
