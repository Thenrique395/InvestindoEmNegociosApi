using FluentAssertions;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Goals;

namespace InvestindoEmNegocio.Tests;

public class GoalProgressCalculatorTests
{
    private static readonly DateOnly Start = new(2026, 7, 1);
    private static readonly DateOnly End = new(2026, 7, 31);

    // ---- Despesa (limite): consumir NÃO é sucesso -------------------------

    [Fact]
    public void Expense_Below_Warning_Is_OnTrack_With_Available()
    {
        var p = GoalProgressCalculator.Calculate(GoalKind.Expense, 1000m, 720m, 0m, Start, End, new DateOnly(2026, 7, 21), warningThreshold: 80m);
        p.Percent.Should().Be(72m);
        p.Remaining.Should().Be(280m); // disponível
        p.State.Should().Be(CalculatedGoalState.OnTrack);
    }

    [Fact]
    public void Expense_At_Or_Above_Warning_Is_Attention()
    {
        var p = GoalProgressCalculator.Calculate(GoalKind.Expense, 1000m, 850m, 0m, Start, End, new DateOnly(2026, 7, 21), warningThreshold: 80m);
        p.State.Should().Be(CalculatedGoalState.Attention);
    }

    [Fact]
    public void Expense_Above_Limit_Is_Exceeded()
    {
        var p = GoalProgressCalculator.Calculate(GoalKind.Expense, 1000m, 1100m, 0m, Start, End, new DateOnly(2026, 7, 21));
        p.Percent.Should().Be(110m);
        p.Remaining.Should().Be(0m);
        p.State.Should().Be(CalculatedGoalState.Exceeded);
    }

    [Fact]
    public void Expense_Never_Achieved_By_Consumption()
    {
        var p = GoalProgressCalculator.Calculate(GoalKind.Expense, 1000m, 1000m, 0m, Start, End, new DateOnly(2026, 7, 21));
        p.State.Should().NotBe(CalculatedGoalState.Achieved);
    }

    // ---- Receita/Investimento (alvo): aproximar-se é positivo -------------

    [Fact]
    public void Income_Below_Target_Reports_Remaining_And_Percent()
    {
        var p = GoalProgressCalculator.Calculate(GoalKind.Income, 10000m, 7500m, 2000m, Start, End, new DateOnly(2026, 7, 21));
        p.Percent.Should().Be(75m);
        p.Remaining.Should().Be(2500m);
        p.Pending.Should().Be(2000m); // previsto separado, fora do percentual
    }

    [Fact]
    public void Income_At_Target_Is_Achieved()
    {
        var p = GoalProgressCalculator.Calculate(GoalKind.Income, 10000m, 10000m, 0m, Start, End, new DateOnly(2026, 7, 21));
        p.State.Should().Be(CalculatedGoalState.Achieved);
    }

    [Fact]
    public void Income_Period_Ended_Below_Target_Is_Overdue()
    {
        var p = GoalProgressCalculator.Calculate(GoalKind.Income, 10000m, 6000m, 0m, Start, End, new DateOnly(2026, 8, 5));
        p.State.Should().Be(CalculatedGoalState.Overdue);
    }

    [Fact]
    public void Income_Behind_Pace_Is_Attention()
    {
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 12, 31);
        var p = GoalProgressCalculator.Calculate(GoalKind.Income, 12000m, 3000m, 0m, start, end, new DateOnly(2026, 7, 1), warningThreshold: 80m);
        p.State.Should().Be(CalculatedGoalState.Attention);
    }

    // ---- Projeção / dias --------------------------------------------------

    [Fact]
    public void Forecast_Projects_By_Current_Pace()
    {
        // 31 dias no período; 10 decorridos; realized 300 -> 300/10*31 = 930
        var p = GoalProgressCalculator.Calculate(GoalKind.Expense, 1000m, 300m, 0m, Start, End, new DateOnly(2026, 7, 10));
        p.Forecast.Should().Be(930m);
        p.DaysRemaining.Should().Be(21);
    }

    [Fact]
    public void Zero_Target_Yields_Zero_Percent()
    {
        var p = GoalProgressCalculator.Calculate(GoalKind.Income, 0m, 500m, 0m, null, null, new DateOnly(2026, 7, 10));
        p.Percent.Should().Be(0m);
        p.Forecast.Should().BeNull();
        p.DaysRemaining.Should().BeNull();
    }
}
