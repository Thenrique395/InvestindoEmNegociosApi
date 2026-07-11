using FluentAssertions;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Goals;

namespace InvestindoEmNegocio.Tests;

public class GoalAlertEvaluatorTests
{
    private static GoalProgress Progress(CalculatedGoalState state, decimal percent) =>
        new(1000m, percent * 10m, 0m, percent, 0m, null, null, state);

    private static GoalAlertDescriptor? Eval(GoalKind kind, CalculatedGoalState state, decimal percent) =>
        GoalAlertEvaluator.Evaluate(Guid.NewGuid(), "Alimentação", kind, Progress(state, percent), "20260701");

    [Fact]
    public void Expense_Exceeded_Emits_Exceeded_Alert()
    {
        var d = Eval(GoalKind.Expense, CalculatedGoalState.Exceeded, 110);
        d!.Kind.Should().Be(NotificationKind.GoalExceeded);
        d.ReferenceKey.Should().StartWith("goal-exceeded:");
        d.Message.Should().Contain("110%");
    }

    [Fact]
    public void Expense_Attention_Emits_Warning()
    {
        var d = Eval(GoalKind.Expense, CalculatedGoalState.Attention, 85);
        d!.Kind.Should().Be(NotificationKind.GoalWarning);
        d.Title.Should().Be("Atenção ao limite");
    }

    [Fact]
    public void Income_Attention_Emits_Behind_Pace()
    {
        var d = Eval(GoalKind.Income, CalculatedGoalState.Attention, 40);
        d!.Kind.Should().Be(NotificationKind.GoalWarning);
        d.ReferenceKey.Should().StartWith("goal-behind:");
    }

    [Fact]
    public void Overdue_Emits_Overdue_Alert()
    {
        var d = Eval(GoalKind.Income, CalculatedGoalState.Overdue, 60);
        d!.Kind.Should().Be(NotificationKind.GoalOverdue);
    }

    [Fact]
    public void Achieved_Emits_Achieved_Alert()
    {
        var d = Eval(GoalKind.Investment, CalculatedGoalState.Achieved, 100);
        d!.Kind.Should().Be(NotificationKind.GoalAchieved);
        d.ReferenceKey.Should().StartWith("goal-achieved:");
    }

    [Fact]
    public void OnTrack_Emits_Nothing()
    {
        Eval(GoalKind.Expense, CalculatedGoalState.OnTrack, 30).Should().BeNull();
    }

    [Fact]
    public void ReferenceKey_Includes_Period_For_Dedup()
    {
        var id = Guid.NewGuid();
        var july = GoalAlertEvaluator.Evaluate(id, "M", GoalKind.Expense, Progress(CalculatedGoalState.Exceeded, 110), "20260701");
        var august = GoalAlertEvaluator.Evaluate(id, "M", GoalKind.Expense, Progress(CalculatedGoalState.Exceeded, 110), "20260801");
        july!.ReferenceKey.Should().NotBe(august!.ReferenceKey);
    }
}
