namespace InvestindoEmNegocio.Application.DTOs;

public record PreferencesDto(string Currency, List<string> Locales, NotificationPreferencesDto Notifications);

public record NotificationPreferencesDto(
    bool UpcomingEnabled,
    bool OverdueEnabled,
    bool InAppEnabled,
    bool EmailEnabled,
    int DaysBeforeDue
);
