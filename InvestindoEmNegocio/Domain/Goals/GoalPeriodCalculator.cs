using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Goals;

/// <summary>
/// Cálculo puro das janelas de período (ocorrências) de uma meta a partir da
/// periodicidade e de uma data-âncora. Sequência é 1-based a partir da âncora.
/// </summary>
public static class GoalPeriodCalculator
{
    public sealed record Window(int Sequence, DateOnly Start, DateOnly End);

    /// <summary>Janela (ocorrência) que contém <paramref name="date"/>.</summary>
    public static Window CurrentWindow(RecurrenceType recurrence, DateOnly anchorStart, DateOnly fallbackEnd, DateOnly date)
    {
        var seq = CurrentSequence(recurrence, anchorStart, date);
        return WindowForSequence(recurrence, anchorStart, fallbackEnd, seq);
    }

    public static int CurrentSequence(RecurrenceType recurrence, DateOnly anchorStart, DateOnly date)
    {
        if (recurrence is RecurrenceType.None or RecurrenceType.Custom) return 1;
        if (date < anchorStart) return 1;

        if (recurrence == RecurrenceType.Weekly)
            return ((date.DayNumber - anchorStart.DayNumber) / 7) + 1;

        var blockMonths = BlockMonths(recurrence);
        var anchorMonth = new DateOnly(anchorStart.Year, anchorStart.Month, 1);
        var monthsSince = (date.Year - anchorMonth.Year) * 12 + (date.Month - anchorMonth.Month);
        return (monthsSince / blockMonths) + 1;
    }

    public static Window WindowForSequence(RecurrenceType recurrence, DateOnly anchorStart, DateOnly fallbackEnd, int sequence)
    {
        if (sequence < 1) sequence = 1;

        if (recurrence is RecurrenceType.None or RecurrenceType.Custom)
            return new Window(1, anchorStart, fallbackEnd);

        if (recurrence == RecurrenceType.Weekly)
        {
            var start = anchorStart.AddDays((sequence - 1) * 7);
            return new Window(sequence, start, start.AddDays(6));
        }

        var blockMonths = BlockMonths(recurrence);
        var anchorMonth = new DateOnly(anchorStart.Year, anchorStart.Month, 1);
        var start2 = anchorMonth.AddMonths((sequence - 1) * blockMonths);
        var end2 = start2.AddMonths(blockMonths).AddDays(-1);
        return new Window(sequence, start2, end2);
    }

    private static int BlockMonths(RecurrenceType recurrence) => recurrence switch
    {
        RecurrenceType.Monthly => 1,
        RecurrenceType.Quarterly => 3,
        RecurrenceType.Semiannual => 6,
        RecurrenceType.Annual => 12,
        _ => 1
    };
}
