namespace InvestindoEmNegocio.Application.Services;

public static class CardStatementCycleCalculator
{
    public static CardStatementCycle Calculate(DateOnly purchaseDate, int statementCloseDay, int dueDay)
    {
        var closeThisMonth = BuildDate(purchaseDate.Year, purchaseDate.Month, statementCloseDay);

        var statementMonthRef = purchaseDate <= closeThisMonth
            ? purchaseDate
            : purchaseDate.AddMonths(1);

        var statementCloseDate = BuildDate(statementMonthRef.Year, statementMonthRef.Month, statementCloseDay);

        var dueCandidate = BuildDate(statementMonthRef.Year, statementMonthRef.Month, dueDay);
        if (dueCandidate <= statementCloseDate)
        {
            var next = statementMonthRef.AddMonths(1);
            dueCandidate = BuildDate(next.Year, next.Month, dueDay);
        }

        return new CardStatementCycle(
            StatementYear: statementMonthRef.Year,
            StatementMonth: statementMonthRef.Month,
            StatementCloseDate: statementCloseDate,
            StatementDueDate: dueCandidate);
    }

    private static DateOnly BuildDate(int year, int month, int day)
    {
        var maxDay = DateTime.DaysInMonth(year, month);
        return new DateOnly(year, month, Math.Min(Math.Max(day, 1), maxDay));
    }
}

public sealed record CardStatementCycle(
    int StatementYear,
    int StatementMonth,
    DateOnly StatementCloseDate,
    DateOnly StatementDueDate);
