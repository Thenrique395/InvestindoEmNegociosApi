using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace InvestindoEmNegocio.Application.Services;

public sealed class AuthRegistrationApplicationService(
    IAuthService authService,
    ILogger<AuthRegistrationApplicationService> logger) : IAuthRegistrationApplicationService
{
    public async Task<AuthResponse> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await authService.RegisterAsync(request, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Falha de validação ao registrar usuário para {Email}", request.Email);
            throw new AppProblemException("Cadastro inválido", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Conflito de registro para {Email}", request.Email);
            throw new AppProblemException("Cadastro inválido", ex.Message, StatusCodes.Status409Conflict);
        }
    }
}
