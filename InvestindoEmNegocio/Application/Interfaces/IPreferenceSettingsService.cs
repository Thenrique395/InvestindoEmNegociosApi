using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IPreferenceSettingsService
{
    Task<PreferencesDto> GetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<PreferencesDto> UpdateAsync(Guid userId, UpdatePreferencesRequest request, CancellationToken cancellationToken = default);
}
