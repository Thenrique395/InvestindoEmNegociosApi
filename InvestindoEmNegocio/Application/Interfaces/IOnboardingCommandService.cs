using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IOnboardingCommandService
{
    Task<OnboardingStatusDto> UpdateStatusAsync(Guid userId, UpdateOnboardingRequest request, CancellationToken cancellationToken = default);
}
