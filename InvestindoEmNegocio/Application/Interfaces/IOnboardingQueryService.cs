using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IOnboardingQueryService
{
    Task<OnboardingStatusDto> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default);
}
