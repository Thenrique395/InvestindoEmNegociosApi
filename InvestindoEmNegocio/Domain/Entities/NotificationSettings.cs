namespace InvestindoEmNegocio.Domain.Entities;

public class NotificationSettings
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public bool IncomeUpcomingEnabled { get; private set; } = true;
    public int IncomeDaysBefore { get; private set; } = 2;
    public bool ExpenseUpcomingEnabled { get; private set; } = true;
    public int ExpenseDaysBefore { get; private set; } = 2;
    public bool ExpenseOverdueEnabled { get; private set; } = true;
    public bool CardCloseSoonEnabled { get; private set; } = true;
    public int CardCloseDaysBefore { get; private set; } = 2;
    public bool CardCloseDayEnabled { get; private set; } = true;
    public bool MonthCloseEnabled { get; private set; } = true;
    public bool MonthSummaryEnabled { get; private set; } = true;
    public bool GoalBelowExpectedEnabled { get; private set; } = true;
    public bool GoalCompletedEnabled { get; private set; } = true;
    public bool GoalInactivityEnabled { get; private set; } = true;
    public int GoalInactivityDays { get; private set; } = 30;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private NotificationSettings() { }

    public NotificationSettings(
        bool incomeUpcomingEnabled,
        int incomeDaysBefore,
        bool expenseUpcomingEnabled,
        int expenseDaysBefore,
        bool expenseOverdueEnabled,
        bool cardCloseSoonEnabled,
        int cardCloseDaysBefore,
        bool cardCloseDayEnabled,
        bool monthCloseEnabled,
        bool monthSummaryEnabled,
        bool goalBelowExpectedEnabled,
        bool goalCompletedEnabled,
        bool goalInactivityEnabled,
        int goalInactivityDays)
    {
        Update(
            incomeUpcomingEnabled,
            incomeDaysBefore,
            expenseUpcomingEnabled,
            expenseDaysBefore,
            expenseOverdueEnabled,
            cardCloseSoonEnabled,
            cardCloseDaysBefore,
            cardCloseDayEnabled,
            monthCloseEnabled,
            monthSummaryEnabled,
            goalBelowExpectedEnabled,
            goalCompletedEnabled,
            goalInactivityEnabled,
            goalInactivityDays);
    }

    public void Update(
        bool incomeUpcomingEnabled,
        int incomeDaysBefore,
        bool expenseUpcomingEnabled,
        int expenseDaysBefore,
        bool expenseOverdueEnabled,
        bool cardCloseSoonEnabled,
        int cardCloseDaysBefore,
        bool cardCloseDayEnabled,
        bool monthCloseEnabled,
        bool monthSummaryEnabled,
        bool goalBelowExpectedEnabled,
        bool goalCompletedEnabled,
        bool goalInactivityEnabled,
        int goalInactivityDays)
    {
        IncomeUpcomingEnabled = incomeUpcomingEnabled;
        IncomeDaysBefore = Math.Max(0, incomeDaysBefore);
        ExpenseUpcomingEnabled = expenseUpcomingEnabled;
        ExpenseDaysBefore = Math.Max(0, expenseDaysBefore);
        ExpenseOverdueEnabled = expenseOverdueEnabled;
        CardCloseSoonEnabled = cardCloseSoonEnabled;
        CardCloseDaysBefore = Math.Max(0, cardCloseDaysBefore);
        CardCloseDayEnabled = cardCloseDayEnabled;
        MonthCloseEnabled = monthCloseEnabled;
        MonthSummaryEnabled = monthSummaryEnabled;
        GoalBelowExpectedEnabled = goalBelowExpectedEnabled;
        GoalCompletedEnabled = goalCompletedEnabled;
        GoalInactivityEnabled = goalInactivityEnabled;
        GoalInactivityDays = Math.Max(0, goalInactivityDays);
        UpdatedAt = DateTime.UtcNow;
    }
}
