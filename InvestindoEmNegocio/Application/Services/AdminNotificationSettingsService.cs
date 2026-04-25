using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public sealed class AdminNotificationSettingsService(INotificationSettingsRepository notificationSettingsRepository) : IAdminNotificationSettingsService
{
    public async Task<NotificationSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await notificationSettingsRepository.GetOrCreateAsync(cancellationToken);
        return ToDto(settings);
    }

    public async Task<NotificationSettingsDto> UpdateAsync(UpdateNotificationSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await notificationSettingsRepository.GetOrCreateAsync(cancellationToken);
        settings.Update(
            request.IncomeUpcomingEnabled,
            request.IncomeDaysBefore,
            request.ExpenseUpcomingEnabled,
            request.ExpenseDaysBefore,
            request.ExpenseOverdueEnabled,
            request.CardCloseSoonEnabled,
            request.CardCloseDaysBefore,
            request.CardCloseDayEnabled,
            request.MonthCloseEnabled,
            request.MonthSummaryEnabled,
            request.GoalBelowExpectedEnabled,
            request.GoalCompletedEnabled,
            request.GoalInactivityEnabled,
            request.GoalInactivityDays);
        await notificationSettingsRepository.SaveChangesAsync(cancellationToken);
        return ToDto(settings);
    }

    private static NotificationSettingsDto ToDto(NotificationSettings settings) =>
        new(
            settings.IncomeUpcomingEnabled,
            settings.IncomeDaysBefore,
            settings.ExpenseUpcomingEnabled,
            settings.ExpenseDaysBefore,
            settings.ExpenseOverdueEnabled,
            settings.CardCloseSoonEnabled,
            settings.CardCloseDaysBefore,
            settings.CardCloseDayEnabled,
            settings.MonthCloseEnabled,
            settings.MonthSummaryEnabled,
            settings.GoalBelowExpectedEnabled,
            settings.GoalCompletedEnabled,
            settings.GoalInactivityEnabled,
            settings.GoalInactivityDays);
}
