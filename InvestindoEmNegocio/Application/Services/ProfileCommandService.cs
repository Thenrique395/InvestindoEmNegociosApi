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
            var document = user?.Document ?? string.Empty;
            var existing = await profileRepository.GetByUserIdAsync(userId, cancellationToken);
            if (existing is null)
            {
                var profile = new UserProfile(userId, request.FullName, request.Phone, request.BirthDate,
                    request.AvatarUrl, request.City, request.State, request.Country, request.Language, "BRL", request.CarryOverDay, request.FinancialGoal, request.IntelligenceMode);
                await profileRepository.AddAsync(profile, cancellationToken);
                await profileRepository.SaveChangesAsync(cancellationToken);
                cache.Remove(ProfileQueryService.CacheKey(userId));
                logger.LogInformation("User profile created {UserId}", userId);
                return ProfileDtoFactory.CreateDto(profile, document);
            }

            existing.UpdateProfileData(request.FullName, request.Phone, request.BirthDate, request.AvatarUrl,
                request.City, request.State, request.Country, request.Language, existing.Currency, request.CarryOverDay, request.FinancialGoal, request.IntelligenceMode);
            await profileRepository.SaveChangesAsync(cancellationToken);
            cache.Remove(ProfileQueryService.CacheKey(userId));
            logger.LogInformation("User profile updated {UserId}", userId);
            return ProfileDtoFactory.CreateDto(existing, document);
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

        try
        {
            existing.UpdateProfileData(
                existing.FullName,
                existing.Phone,
                existing.BirthDate,
                avatarUrl,
                existing.City,
                existing.State,
                existing.Country,
                existing.Language,
                existing.Currency,
                existing.CarryOverDay,
                existing.FinancialGoal,
                existing.IntelligenceMode);
            await profileRepository.SaveChangesAsync(cancellationToken);
            cache.Remove(ProfileQueryService.CacheKey(userId));
            logger.LogInformation("User avatar updated {UserId}", userId);
            var user = await userRepository.GetByIdAsync(userId, cancellationToken);
            return ProfileDtoFactory.CreateDto(existing, user?.Document ?? string.Empty);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Atualização de avatar inválida para {UserId}", userId);
            throw new AppProblemException("Perfil inválido", ex.Message, StatusCodes.Status400BadRequest);
        }
    }
}
