using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public class PreferencesService : IPreferencesService
{
    private readonly IUserProfileRepository _profileRepository;

    public PreferencesService(IUserProfileRepository profileRepository)
    {
        _profileRepository = profileRepository;
    }

    public async Task<PreferencesDto> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await _profileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return new PreferencesDto("BRL", new List<string> { "pt-BR" });
        }

        var locales = (profile.Language ?? "pt-BR").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (locales.Count == 0) locales.Add("pt-BR");
        return new PreferencesDto(profile.Currency ?? "BRL", locales);
    }

    public async Task<PreferencesDto> UpdateAsync(Guid userId, UpdatePreferencesRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await _profileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            var primaryLocale = request.Locales.FirstOrDefault() ?? "pt-BR";
            profile = new UserProfile(userId, string.Empty, string.Empty, string.Empty, null, string.Empty, string.Empty, string.Empty, string.Empty, primaryLocale, request.Currency);
            await _profileRepository.AddAsync(profile, cancellationToken);
        }

        profile.GetType().GetProperty("Language")?.SetValue(profile, string.Join(';', request.Locales));
        profile.GetType().GetProperty("Currency")?.SetValue(profile, request.Currency);
        profile.GetType().GetProperty("UpdatedAt")?.SetValue(profile, DateTime.UtcNow);

        await _profileRepository.SaveChangesAsync(cancellationToken);
        return new PreferencesDto(request.Currency, request.Locales);
    }
}
