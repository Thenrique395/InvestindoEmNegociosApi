using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IOnboardingService
{
    Task<OnboardingStatusDto> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<OnboardingStatusDto> UpdateStatusAsync(Guid userId, UpdateOnboardingRequest request, CancellationToken cancellationToken = default);
}
