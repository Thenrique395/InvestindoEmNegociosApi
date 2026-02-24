using FluentAssertions;
using InvestindoEmNegocio.Application.Services;

namespace InvestindoEmNegocio.Tests;

public class CardStatementCycleCalculatorTests
{
    [Fact]
    public void Calculate_Should_Use_Same_Month_When_Purchase_Before_Closing()
    {
        var cycle = CardStatementCycleCalculator.Calculate(
            purchaseDate: new DateOnly(2026, 2, 5),
            statementCloseDay: 8,
            dueDay: 15);

        cycle.StatementYear.Should().Be(2026);
        cycle.StatementMonth.Should().Be(2);
        cycle.StatementCloseDate.Should().Be(new DateOnly(2026, 2, 8));
        cycle.StatementDueDate.Should().Be(new DateOnly(2026, 2, 15));
    }

    [Fact]
    public void Calculate_Should_Move_To_Next_Month_When_Purchase_After_Closing()
    {
        var cycle = CardStatementCycleCalculator.Calculate(
            purchaseDate: new DateOnly(2026, 2, 9),
            statementCloseDay: 8,
            dueDay: 15);

        cycle.StatementYear.Should().Be(2026);
        cycle.StatementMonth.Should().Be(3);
        cycle.StatementCloseDate.Should().Be(new DateOnly(2026, 3, 8));
        cycle.StatementDueDate.Should().Be(new DateOnly(2026, 3, 15));
    }

    [Fact]
    public void Calculate_Should_Push_Due_To_Next_Month_When_Due_Day_Is_Before_Close_Day()
    {
        var cycle = CardStatementCycleCalculator.Calculate(
            purchaseDate: new DateOnly(2026, 2, 20),
            statementCloseDay: 25,
            dueDay: 10);

        cycle.StatementYear.Should().Be(2026);
        cycle.StatementMonth.Should().Be(2);
        cycle.StatementCloseDate.Should().Be(new DateOnly(2026, 2, 25));
        cycle.StatementDueDate.Should().Be(new DateOnly(2026, 3, 10));
    }

    [Fact]
    public void Calculate_Should_Clamp_Days_For_Short_Months()
    {
        var cycle = CardStatementCycleCalculator.Calculate(
            purchaseDate: new DateOnly(2026, 2, 27),
            statementCloseDay: 31,
            dueDay: 31);

        cycle.StatementYear.Should().Be(2026);
        cycle.StatementMonth.Should().Be(2);
        cycle.StatementCloseDate.Should().Be(new DateOnly(2026, 2, 28));
        cycle.StatementDueDate.Should().Be(new DateOnly(2026, 3, 31));
    }
}
