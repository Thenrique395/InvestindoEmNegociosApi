using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using System.Text.RegularExpressions;
using System.Linq;

namespace InvestindoEmNegocio.Application.Services;

public class ProfileService : IProfileService
{
    private readonly IUserProfileRepository _profileRepository;

    public ProfileService(IUserProfileRepository profileRepository)
    {
        _profileRepository = profileRepository;
    }

    public async Task<UserProfileDto?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await _profileRepository.GetByUserIdAsync(userId, cancellationToken);
        return profile is null ? null : Map(profile);
    }

    public async Task<UserProfileDto> UpsertAsync(Guid userId, UpsertUserProfileRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _profileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (existing is null)
        {
            var profile = new UserProfile(userId, request.FullName, request.Document, request.Phone, request.BirthDate, request.AvatarUrl, request.City, request.State, request.Country, request.Language);
            await _profileRepository.AddAsync(profile, cancellationToken);
            await _profileRepository.SaveChangesAsync(cancellationToken);
            return Map(profile);
        }

        existing.SetData(request.FullName, request.Document, request.Phone, request.BirthDate, request.AvatarUrl, request.City, request.State, request.Country, request.Language);
        await _profileRepository.SaveChangesAsync(cancellationToken);
        return Map(existing);
    }

    public async Task<UserProfileDto> UpdateAvatarAsync(Guid userId, string avatarUrl, CancellationToken cancellationToken = default)
    {
        var existing = await _profileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (existing is null)
        {
            throw new ArgumentException("Perfil não encontrado. Preencha seus dados antes de enviar a foto.");
        }

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
        await _profileRepository.SaveChangesAsync(cancellationToken);
        return Map(existing);
    }

    private static UserProfileDto Map(UserProfile profile)
    {
        var locales = (profile.Language ?? "pt-BR").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (locales.Count == 0) locales.Add("pt-BR");
        var currency = profile.Currency ?? "BRL";
        return new UserProfileDto(profile.UserId, profile.FullName, profile.Document, profile.Phone, profile.BirthDate, profile.AvatarUrl, profile.City, profile.State, profile.Country, profile.Language, currency, locales);
    }
}
