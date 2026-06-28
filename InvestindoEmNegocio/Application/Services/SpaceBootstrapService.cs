using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

public sealed class SpaceBootstrapService(
    ISpaceRepository spaceRepository,
    ILogger<SpaceBootstrapService> logger) : ISpaceBootstrapService
{
    public async Task<Guid> EnsureDefaultSpaceAsync(User user, CancellationToken cancellationToken = default)
    {
        var existing = await spaceRepository.GetDefaultByUserAsync(user.Id, cancellationToken);
        if (existing is not null)
            return existing.Id;

        var space = new Space(user.Id, "Espaço Principal", isDefault: true);
        await spaceRepository.AddAsync(space, cancellationToken);
        await spaceRepository.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Default space created for user {UserId}", user.Id);
        return space.Id;
    }
}
