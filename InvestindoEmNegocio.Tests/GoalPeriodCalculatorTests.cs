using FluentAssertions;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Goals;

namespace InvestindoEmNegocio.Tests;

public class GoalPeriodCalculatorTests
{
    [Fact]
    public void Monthly_Window_Is_Calendar_Month()
    {
        var w = GoalPeriodCalculator.CurrentWindow(RecurrenceType.Monthly, new DateOnly(2026, 1, 15), new DateOnly(2026, 12, 31), new DateOnly(2026, 3, 10));
        w.Sequence.Should().Be(3);
        w.Start.Should().Be(new DateOnly(2026, 3, 1));
        w.End.Should().Be(new DateOnly(2026, 3, 31));
    }

    [Fact]
    public void Weekly_Window_Is_Seven_Days_From_Anchor()
    {
        var w = GoalPeriodCalculator.CurrentWindow(RecurrenceType.Weekly, new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 31), new DateOnly(2026, 7, 16));
        w.Sequence.Should().Be(3);
        w.Start.Should().Be(new DateOnly(2026, 7, 15));
        w.End.Should().Be(new DateOnly(2026, 7, 21));
    }

    [Fact]
    public void Quarterly_Window_Is_Three_Month_Block()
    {
        var w = GoalPeriodCalculator.CurrentWindow(RecurrenceType.Quarterly, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), new DateOnly(2026, 5, 10));
        w.Sequence.Should().Be(2);
        w.Start.Should().Be(new DateOnly(2026, 4, 1));
        w.End.Should().Be(new DateOnly(2026, 6, 30));
    }

    [Fact]
    public void None_Is_Single_Window()
    {
        var w = GoalPeriodCalculator.CurrentWindow(RecurrenceType.None, new DateOnly(2026, 3, 1), new DateOnly(2026, 9, 30), new DateOnly(2026, 5, 10));
        w.Sequence.Should().Be(1);
        w.Start.Should().Be(new DateOnly(2026, 3, 1));
        w.End.Should().Be(new DateOnly(2026, 9, 30));
    }

    [Fact]
    public void Before_Anchor_Returns_First_Sequence()
    {
        GoalPeriodCalculator.CurrentSequence(RecurrenceType.Monthly, new DateOnly(2026, 6, 1), new DateOnly(2026, 1, 1)).Should().Be(1);
    }
}
