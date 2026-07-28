using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAuthRegistrationService
{
    Task<RegisteredUserResponse> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default);
}
