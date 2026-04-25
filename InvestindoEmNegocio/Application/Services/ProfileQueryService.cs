using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace InvestindoEmNegocio.Application.Services;

public sealed class ProfileQueryService(
    IUserProfileRepository profileRepository,
    IMemoryCache cache) : IProfileQueryService
{
    public async Task<UserProfileDto?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKey(userId);
        if (cache.TryGetValue(cacheKey, out UserProfileDto? cached))
            return cached;

        var profile = await profileRepository.GetByUserIdAsync(userId, cancellationToken);
        var mapped = profile is null ? null : ProfileDtoFactory.CreateDto(profile);
        if (mapped is not null)
            cache.Set(cacheKey, mapped, TimeSpan.FromSeconds(15));

        return mapped;
    }

    internal static string CacheKey(Guid userId) => $"profile:{userId:N}";
}
