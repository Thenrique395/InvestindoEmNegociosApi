using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace InvestindoEmNegocio.Application.Services;

public sealed class ProfileQueryService(
    IUserProfileRepository profileRepository,
    IUserRepository userRepository,
    IMemoryCache cache) : IProfileQueryService
{
    public async Task<UserProfileDto?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKey(userId);
        if (cache.TryGetValue(cacheKey, out UserProfileDto? cached))
            return cached;

        var profile = await profileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null) return null;

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        var mapped = ProfileDtoFactory.CreateDto(profile, user?.Document ?? string.Empty);
        cache.Set(cacheKey, mapped, TimeSpan.FromSeconds(15));

        return mapped;
    }

    internal static string CacheKey(Guid userId) => $"profile:{userId:N}";
}
