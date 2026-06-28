using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

using BCryptNet = BCrypt.Net.BCrypt;

public class SpaceService(
    ISpaceRepository spaceRepository,
    IUserRepository userRepository,
    IUserSessionService userSessionService,
    ILogger<SpaceService> logger) : ISpaceService
{
    private readonly ILogger<SpaceService> _logger = logger;

    public async Task<List<SpaceResponse>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var spaces = await spaceRepository.ListByUserAsync(userId, cancellationToken);
        return spaces.Select(MapToResponse).ToList();
    }

    public async Task<SpaceResponse> CreateAsync(Guid userId, SpaceRequest request, CancellationToken cancellationToken = default)
    {
        var passwordHash = HashOrNull(request.Password);
        var space = new Space(userId, request.Name, isDefault: false, passwordHash: passwordHash);

        await spaceRepository.AddAsync(space, cancellationToken);
        await spaceRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Space created {UserId} {SpaceId}", userId, space.Id);
        return MapToResponse(space);
    }

    public async Task<SpaceResponse?> UpdateAsync(Guid userId, Guid spaceId, SpaceRequest request, CancellationToken cancellationToken = default)
    {
        var space = await spaceRepository.GetByIdAsync(spaceId, userId, cancellationToken);
        if (space is null) return null;

        space.Rename(request.Name);
        space.SetPasswordHash(HashOrNull(request.Password));
        await spaceRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Space updated {UserId} {SpaceId}", userId, space.Id);
        return MapToResponse(space);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid spaceId, CancellationToken cancellationToken = default)
    {
        var space = await spaceRepository.GetByIdAsync(spaceId, userId, cancellationToken);
        if (space is null) return false;

        if (space.IsDefault)
            throw new ArgumentException("O espaço padrão não pode ser excluído.");

        var allSpaces = await spaceRepository.ListByUserAsync(userId, cancellationToken);
        if (allSpaces.Count <= 1)
            throw new ArgumentException("O último espaço não pode ser excluído.");

        space.MarkDeleted(DateTime.UtcNow);
        await spaceRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Space deleted {UserId} {SpaceId}", userId, spaceId);
        return true;
    }

    public async Task<AuthResponse> EnterAsync(Guid userId, Guid spaceId, EnterSpaceRequest request, CancellationToken cancellationToken = default)
    {
        var space = await spaceRepository.GetByIdAsync(spaceId, userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Espaço não encontrado.");

        if (space.HasPassword && !BCryptNet.Verify(request.Password ?? string.Empty, space.PasswordHash))
            throw new UnauthorizedAccessException("Senha do espaço inválida.");

        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Usuário não encontrado.");

        _logger.LogInformation("Space entered {UserId} {SpaceId}", userId, spaceId);
        return await userSessionService.IssueAsync(user, spaceId, cancellationToken);
    }

    private static string? HashOrNull(string? rawPassword) =>
        string.IsNullOrWhiteSpace(rawPassword) ? null : BCryptNet.HashPassword(rawPassword, AuthServicePolicies.BcryptWorkFactor);

    private static SpaceResponse MapToResponse(Space space) =>
        new(space.Id, space.Name, space.IsDefault, space.HasPassword, space.CreatedAt);
}
