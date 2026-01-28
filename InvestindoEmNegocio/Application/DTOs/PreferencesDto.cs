namespace InvestindoEmNegocio.Application.DTOs;

public record PreferencesDto(string Currency, List<string> Locales, NotificationPreferencesDto Notifications);

public record NotificationPreferencesDto(
    bool InAppEnabled,
    bool EmailEnabled
);
