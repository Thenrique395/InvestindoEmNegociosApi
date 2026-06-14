using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace InvestindoEmNegocio.Application.Services;

public sealed class ProfileCommandService(
    IUserProfileRepository profileRepository,
    IUserRepository userRepository,
    IMemoryCache cache,
    ILogger<ProfileCommandService> logger) : IProfileCommandService
{
    public async Task<UserProfileDto> UpsertAsync(Guid userId, UpsertUserProfileRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is not null)
            {
                user.UpdateProfileDetails(request.AvatarUrl, request.City, request.State, request.Country);
                user.UpdateIdentityDetails(request.FullName, request.Phone, request.BirthDate);
                await userRepository.SaveChangesAsync(cancellationToken);
            }

            var existing = await profileRepository.GetByUserIdAsync(userId, cancellationToken);
            if (existing is null)
            {
                var profile = new UserProfile(userId,
                    request.Language, "BRL", request.CarryOverDay, request.FinancialGoal, request.IntelligenceMode);
                await profileRepository.AddAsync(profile, cancellationToken);
                await profileRepository.SaveChangesAsync(cancellationToken);
                cache.Remove(ProfileQueryService.CacheKey(userId));
                logger.LogInformation("User profile created {UserId}", userId);
                return ProfileDtoFactory.CreateDto(profile, user);
            }

            existing.UpdateProfileData(request.Language, existing.Currency, request.CarryOverDay, request.FinancialGoal, request.IntelligenceMode);
            await profileRepository.SaveChangesAsync(cancellationToken);
            cache.Remove(ProfileQueryService.CacheKey(userId));
            logger.LogInformation("User profile updated {UserId}", userId);
            return ProfileDtoFactory.CreateDto(existing, user);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Profile upsert inválido para {UserId}", userId);
            throw new AppProblemException("Perfil inválido", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<UserProfileDto> UpdateAvatarAsync(Guid userId, string avatarUrl, CancellationToken cancellationToken = default)
    {
        var existing = await profileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (existing is null)
        {
            throw new AppProblemException(
                "Perfil não encontrado",
                "Preencha seus dados antes de enviar a foto.",
                StatusCodes.Status404NotFound);
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new AppProblemException(
                "Perfil não encontrado",
                "Preencha seus dados antes de enviar a foto.",
                StatusCodes.Status404NotFound);
        }

        user.UpdateProfileDetails(avatarUrl, user.City, user.State, user.Country);
        await userRepository.SaveChangesAsync(cancellationToken);
        cache.Remove(ProfileQueryService.CacheKey(userId));
        logger.LogInformation("User avatar updated {UserId}", userId);
        return ProfileDtoFactory.CreateDto(existing, user);
    }
}
