using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

public class OnboardingService(IUserOnboardingRepository repository, ILogger<OnboardingService> logger) : IOnboardingService
{
    private readonly ILogger<OnboardingService> _logger = logger;
    public async Task<OnboardingStatusDto> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var onboarding = await repository.GetByUserAsync(userId, cancellationToken);
        return onboarding is null
            ? new OnboardingStatusDto(0, false)
            : new OnboardingStatusDto(onboarding.Step, onboarding.Completed);
    }

    public async Task<OnboardingStatusDto> UpdateStatusAsync(Guid userId, UpdateOnboardingRequest request,
        CancellationToken cancellationToken = default)
    {
        var step = Math.Max(0, Math.Min(2, request.Step));
        var onboarding = await repository.GetByUserAsync(userId, cancellationToken);
        if (onboarding is null)
        {
            onboarding = new UserOnboarding(userId, step, request.Completed);
            await repository.AddAsync(onboarding, cancellationToken);
        }
        else
            onboarding.Update(step, request.Completed);


        await repository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Onboarding updated {UserId} {Step} {Completed}", userId, onboarding.Step, onboarding.Completed);
        return new OnboardingStatusDto(onboarding.Step, onboarding.Completed);
    }
}
