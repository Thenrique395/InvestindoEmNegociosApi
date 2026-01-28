namespace InvestindoEmNegocio.Application.DTOs;

public record NotificationSettingsDto(
    bool IncomeUpcomingEnabled,
    int IncomeDaysBefore,
    bool ExpenseUpcomingEnabled,
    int ExpenseDaysBefore,
    bool ExpenseOverdueEnabled,
    bool CardCloseSoonEnabled,
    int CardCloseDaysBefore,
    bool CardCloseDayEnabled,
    bool MonthCloseEnabled,
    bool MonthSummaryEnabled,
    bool GoalBelowExpectedEnabled,
    bool GoalCompletedEnabled,
    bool GoalInactivityEnabled,
    int GoalInactivityDays
);

public record UpdateNotificationSettingsRequest(
    bool IncomeUpcomingEnabled,
    int IncomeDaysBefore,
    bool ExpenseUpcomingEnabled,
    int ExpenseDaysBefore,
    bool ExpenseOverdueEnabled,
    bool CardCloseSoonEnabled,
    int CardCloseDaysBefore,
    bool CardCloseDayEnabled,
    bool MonthCloseEnabled,
    bool MonthSummaryEnabled,
    bool GoalBelowExpectedEnabled,
    bool GoalCompletedEnabled,
    bool GoalInactivityEnabled,
    int GoalInactivityDays
);
