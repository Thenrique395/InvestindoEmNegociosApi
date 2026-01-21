using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Domain.Repositories;

public interface IUserOnboardingRepository
{
    Task<UserOnboarding?> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(UserOnboarding onboarding, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
