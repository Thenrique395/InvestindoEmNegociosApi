namespace InvestindoEmNegocio.Domain.Finance;

public static class CompetenceWindowCalculator
{
    public static (DateOnly Start, DateOnly End) Resolve(DateOnly referenceDate, int carryOverDay)
    {
        var currentMonthStart = BuildSafeDate(referenceDate.Year, referenceDate.Month, carryOverDay);
        var start = referenceDate >= currentMonthStart
            ? currentMonthStart
            : BuildSafeDate(referenceDate.AddMonths(-1).Year, referenceDate.AddMonths(-1).Month, carryOverDay);

        var nextStartRef = start.AddMonths(1);
        var nextStart = BuildSafeDate(nextStartRef.Year, nextStartRef.Month, carryOverDay);
        var end = nextStart.AddDays(-1);
        return (start, end);
    }

    public static DateOnly BuildSafeDate(int year, int month, int day)
    {
        var normalized = Math.Clamp(day, 1, 31);
        var maxDay = DateTime.DaysInMonth(year, month);
        return new DateOnly(year, month, Math.Min(normalized, maxDay));
    }
}

