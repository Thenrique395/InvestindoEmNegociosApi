using FluentAssertions;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Tests;

public class GoalLifecycleTests
{
    private static Goal NewGoal(GoalKind kind = GoalKind.Income) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Meta", 1000m, 2026, kind: kind);

    [Fact]
    public void Pause_Then_Resume_Toggles_Status()
    {
        var goal = NewGoal();
        goal.Pause();
        goal.Status.Should().Be(GoalStatus.Paused);
        goal.Resume();
        goal.Status.Should().Be(GoalStatus.Active);
    }

    [Fact]
    public void Archive_Sets_Status_And_Timestamp()
    {
        var goal = NewGoal();
        goal.Archive(DateTime.UtcNow);
        goal.Status.Should().Be(GoalStatus.Archived);
        goal.ArchivedAt.Should().NotBeNull();
    }

    [Fact]
    public void CompleteManually_Throws_For_Expense()
    {
        var goal = NewGoal(GoalKind.Expense);
        var act = () => goal.CompleteManually();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ConfigurePlanning_Rejects_Warning_Above_Critical()
    {
        var goal = NewGoal(GoalKind.Expense);
        var act = () => goal.ConfigurePlanning(GoalMode.Limit, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), RecurrenceType.Monthly, 90m, 80m);
        act.Should().Throw<ArgumentException>();
    }
}
