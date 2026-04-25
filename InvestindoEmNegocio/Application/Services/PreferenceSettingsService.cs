using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public sealed class PreferenceSettingsService(
    IUserProfileRepository profileRepository) : IPreferenceSettingsService
{
    private static NotificationPreferencesDto BuildDefaultNotifications() =>
        new(true, true, true, false, 3);

    public async Task<PreferencesDto> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await profileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
            return new PreferencesDto("BRL", new List<string> { "pt-BR" }, BuildDefaultNotifications());

        var locales = (profile.Language ?? "pt-BR")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (locales.Count == 0) locales.Add("pt-BR");
        var notifications = new NotificationPreferencesDto(
            profile.NotifyUpcomingEnabled,
            profile.NotifyOverdueEnabled,
            profile.NotifyInAppEnabled,
            profile.NotifyEmailEnabled,
            profile.NotifyDaysBeforeDue);
        return new PreferencesDto(profile.Currency ?? "BRL", locales, notifications);
    }

    public async Task<PreferencesDto> UpdateAsync(Guid userId, UpdatePreferencesRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await profileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            var primaryLocale = request.Locales.FirstOrDefault() ?? "pt-BR";
            profile = new UserProfile(userId, string.Empty, string.Empty, string.Empty, null, string.Empty,
                string.Empty, string.Empty, string.Empty, primaryLocale, request.Currency);
            await profileRepository.AddAsync(profile, cancellationToken);
        }

        profile.UpdatePreferenceSettings(string.Join(';', request.Locales), request.Currency);
        if (request.Notifications is not null)
        {
            profile.SetNotificationPreferences(
                request.Notifications.UpcomingEnabled,
                request.Notifications.OverdueEnabled,
                request.Notifications.EmailEnabled,
                request.Notifications.InAppEnabled,
                request.Notifications.DaysBeforeDue);
        }

        await profileRepository.SaveChangesAsync(cancellationToken);
        var notifications = request.Notifications ?? BuildDefaultNotifications();
        return new PreferencesDto(request.Currency, request.Locales, notifications);
    }
}
