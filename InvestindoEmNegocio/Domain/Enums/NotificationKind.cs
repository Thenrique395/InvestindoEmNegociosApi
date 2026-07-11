namespace InvestindoEmNegocio.Domain.Enums;

public enum NotificationKind
{
    IncomeUpcoming = 0,
    ExpenseUpcoming = 1,
    ExpenseOverdue = 2,
    CardClosingSoon = 3,
    CardClosingDay = 4,
    MonthClosing = 5,
    MonthSummary = 6,
    GoalBelowExpected = 7,
    GoalCompleted = 8,
    GoalInactive = 9,
    Upcoming = 10,
    Overdue = 11,
    CashflowInsight = 12,
    BillingPending = 13,
    BillingApproved = 14,
    BillingFailed = 15,
    BillingRenewalApproved = 16,
    BillingGracePeriodReminder = 17,
    BillingDowngraded = 18,
    BillingReactivated = 19,
    AiHealthAlert = 20,
    GoalWarning = 21,
    GoalExceeded = 22,
    GoalAchieved = 23,
    GoalOverdue = 24
}
