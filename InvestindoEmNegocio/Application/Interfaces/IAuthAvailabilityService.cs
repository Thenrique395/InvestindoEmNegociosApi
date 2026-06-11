using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAuthAvailabilityService
{
    Task<CheckAvailabilityResponse> CheckAsync(CheckAvailabilityRequest request, CancellationToken cancellationToken = default);
}
