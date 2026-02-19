using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using System.Text.RegularExpressions;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

public class ProfileService(
    IUserProfileRepository profileRepository,
    IMemoryCache cache,
    ILogger<ProfileService> logger) : IProfileService
{
    private readonly ILogger<ProfileService> _logger = logger;
    public async Task<UserProfileDto?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKey(userId);
        if (cache.TryGetValue(cacheKey, out UserProfileDto? cached))
        {
            return cached;
        }

        var profile = await profileRepository.GetByUserIdAsync(userId, cancellationToken);
        var mapped = profile is null ? null : Map(profile);
        if (mapped is not null)
        {
            cache.Set(cacheKey, mapped, TimeSpan.FromSeconds(15));
        }
        return mapped;
    }

    public async Task<UserProfileDto> UpsertAsync(Guid userId, UpsertUserProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await profileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (existing is null)
        {
            var profile = new UserProfile(userId, request.FullName, request.Document, request.Phone, request.BirthDate,
                request.AvatarUrl, request.City, request.State, request.Country, request.Language);
            await profileRepository.AddAsync(profile, cancellationToken);
            await profileRepository.SaveChangesAsync(cancellationToken);
            cache.Remove(CacheKey(userId));
            _logger.LogInformation("User profile created {UserId}", userId);
            return Map(profile);
        }

        existing.SetData(request.FullName, request.Document, request.Phone, request.BirthDate, request.AvatarUrl,
            request.City, request.State, request.Country, request.Language);
        await profileRepository.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey(userId));
        _logger.LogInformation("User profile updated {UserId}", userId);
        return Map(existing);
    }

    public async Task<UserProfileDto> UpdateAvatarAsync(Guid userId, string avatarUrl,
        CancellationToken cancellationToken = default)
    {
        var existing = await profileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (existing is null)
            throw new ArgumentException("Perfil não encontrado. Preencha seus dados antes de enviar a foto.");
        
        existing.SetData(
            existing.FullName,
            existing.Document,
            existing.Phone,
            existing.BirthDate,
            avatarUrl,
            existing.City,
            existing.State,
            existing.Country,
            existing.Language,
            existing.Currency);
        await profileRepository.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey(userId));
        _logger.LogInformation("User avatar updated {UserId}", userId);
        return Map(existing);
    }

    private static string CacheKey(Guid userId) => $"profile:{userId:N}";

    private static UserProfileDto Map(UserProfile profile)
    {
        var locales = (profile.Language ?? "pt-BR")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (locales.Count == 0) locales.Add("pt-BR");
        var currency = profile.Currency ?? "BRL";
        return new UserProfileDto(profile.UserId, profile.FullName, profile.Document, profile.Phone, profile.BirthDate,
            profile.AvatarUrl, profile.City, profile.State, profile.Country, profile.Language, currency, locales);
    }
}
