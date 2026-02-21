using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Repositories;

public class UserOnboardingRepository(InvestDbContext context) : IUserOnboardingRepository
{
    public async Task<UserOnboarding?> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.UserOnboardings.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(UserOnboarding onboarding, CancellationToken cancellationToken = default)
    {
        await context.UserOnboardings.AddAsync(onboarding, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
