using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAuthRegistrationApplicationService
{
    Task<RegisteredUserResponse> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default);
}
