using FluentAssertions;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using InvestindoEmNegocio.Infrastructure.Data;

namespace InvestindoEmNegocio.Tests;

[Trait("Suite", "Smoke")]
public class GoalProgressServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly InvestDbContext _db;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _spaceId = Guid.NewGuid();

    public GoalProgressServiceTests()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<InvestDbContext>().UseSqlite(_connection).Options;
        _db = new InvestDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private Guid AddCategory(MoneyType type)
    {
        var category = new Category(_userId, $"Cat-{Guid.NewGuid():N}", type);
        _db.Categories.Add(category);
        return category.Id;
    }

    private Guid AddPlan(MoneyType type, Guid categoryId)
    {
        var plan = new MoneyPlan(_userId, _spaceId, type, "Plano", 100m, ScheduleType.OneTime, new DateOnly(2026, 7, 1), categoryId: categoryId);
        _db.MoneyPlans.Add(plan);
        return plan.Id;
    }

    private readonly Dictionary<Guid, int> _seq = new();

    private void AddInstallment(Guid planId, DateOnly due, decimal amount, InstallmentStatus status)
    {
        var no = _seq.TryGetValue(planId, out var current) ? current + 1 : 1;
        _seq[planId] = no;
        var inst = new MoneyInstallment(planId, _userId, _spaceId, no, due, amount);
        inst.RestoreStatus(status);
        _db.MoneyInstallments.Add(inst);
    }

    private Goal AddExpenseGoal(decimal target, Guid scopeCategoryId)
    {
        var goal = new Goal(_userId, _spaceId, "Alimentação", target, 2026, kind: GoalKind.Expense);
        goal.ConfigurePlanning(GoalMode.Limit, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), RecurrenceType.Monthly, 80m, 100m);
        goal.ReplaceScopes(new[] { new GoalScope(goal.Id, GoalScopeType.Category, scopeCategoryId) });
        _db.Goals.Add(goal);
        return goal;
    }

    [Fact]
    public async Task Expense_Progress_Counts_Only_Effected_In_Scope_And_Period()
    {
        var catA = AddCategory(MoneyType.Expense);
        var catB = AddCategory(MoneyType.Expense);
        var goal = AddExpenseGoal(1000m, catA);
        var planA = AddPlan(MoneyType.Expense, catA);
        var planB = AddPlan(MoneyType.Expense, catB);
        var incomePlanA = AddPlan(MoneyType.Income, AddCategory(MoneyType.Income));

        AddInstallment(planA, new DateOnly(2026, 7, 10), 700m, InstallmentStatus.Paid);        // conta
        AddInstallment(planA, new DateOnly(2026, 7, 15), 100m, InstallmentStatus.Open);        // pendente
        AddInstallment(planA, new DateOnly(2026, 7, 20), 50m, InstallmentStatus.Canceled);     // cancelada -> ignora
        AddInstallment(planA, new DateOnly(2026, 8, 5), 999m, InstallmentStatus.Paid);         // fora do período -> ignora
        AddInstallment(planB, new DateOnly(2026, 7, 10), 500m, InstallmentStatus.Paid);        // outra categoria -> ignora
        AddInstallment(incomePlanA, new DateOnly(2026, 7, 10), 300m, InstallmentStatus.Paid);  // outro tipo -> ignora
        await _db.SaveChangesAsync();

        var progress = await new GoalProgressService(_db, new GoalRealizedReader(_db)).GetProgressAsync(_userId, goal.Id);

        progress.Should().NotBeNull();
        progress!.Realized.Should().Be(700m);
        progress.Pending.Should().Be(100m);
        progress.Percent.Should().Be(70m);
        progress.State.Should().Be(nameof(CalculatedGoalState.OnTrack));
    }

    [Fact]
    public async Task Income_Progress_Uses_Only_Received()
    {
        var goal = new Goal(_userId, _spaceId, "Receita", 10000m, 2026, kind: GoalKind.Income);
        goal.ConfigurePlanning(GoalMode.Target, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), RecurrenceType.Monthly, null, null);
        _db.Goals.Add(goal); // sem escopo = todas as receitas
        var incomePlan = AddPlan(MoneyType.Income, AddCategory(MoneyType.Income));
        AddInstallment(incomePlan, new DateOnly(2026, 7, 5), 7500m, InstallmentStatus.Paid);
        AddInstallment(incomePlan, new DateOnly(2026, 7, 20), 2000m, InstallmentStatus.Open); // previsto
        await _db.SaveChangesAsync();

        var progress = await new GoalProgressService(_db, new GoalRealizedReader(_db)).GetProgressAsync(_userId, goal.Id);

        progress!.Realized.Should().Be(7500m);
        progress.Pending.Should().Be(2000m);
        progress.Percent.Should().Be(75m);
    }

    [Fact]
    public async Task Investment_Progress_Uses_Contributions()
    {
        var goal = new Goal(_userId, _spaceId, "Aportes", 6000m, 2026, kind: GoalKind.Investment);
        goal.ConfigurePlanning(GoalMode.PeriodContribution, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), RecurrenceType.Annual, null, null);
        _db.Goals.Add(goal);
        _db.GoalContributions.Add(new GoalContribution(goal.Id, _userId, _spaceId, 1500m, new DateOnly(2026, 3, 10)));
        _db.GoalContributions.Add(new GoalContribution(goal.Id, _userId, _spaceId, 500m, new DateOnly(2026, 6, 10)));
        await _db.SaveChangesAsync();

        var progress = await new GoalProgressService(_db, new GoalRealizedReader(_db)).GetProgressAsync(_userId, goal.Id);

        progress!.Realized.Should().Be(2000m);
        progress.Remaining.Should().Be(4000m);
    }

    [Fact]
    public async Task Recurring_Progress_Uses_Current_Month_Window()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var cat = AddCategory(MoneyType.Expense);
        var goal = new Goal(_userId, _spaceId, "Alimentação", 1000m, today.Year, kind: GoalKind.Expense);
        goal.ConfigurePlanning(GoalMode.Limit, monthStart, monthStart.AddMonths(12), RecurrenceType.Monthly, 80m, 100m);
        goal.ReplaceScopes(new[] { new GoalScope(goal.Id, GoalScopeType.Category, cat) });
        _db.Goals.Add(goal);
        var plan = AddPlan(MoneyType.Expense, cat);
        AddInstallment(plan, today, 300m, InstallmentStatus.Paid);
        // mês anterior não deve contar na janela corrente
        AddInstallment(plan, monthStart.AddMonths(-1).AddDays(5), 900m, InstallmentStatus.Paid);
        await _db.SaveChangesAsync();

        var progress = await new GoalProgressService(_db, new GoalRealizedReader(_db)).GetProgressAsync(_userId, goal.Id);

        progress!.Realized.Should().Be(300m);
        progress.Start.Should().Be(monthStart);
    }

    [Fact]
    public async Task Progress_Is_Null_For_Other_User()
    {
        var goal = AddExpenseGoal(1000m, AddCategory(MoneyType.Expense));
        await _db.SaveChangesAsync();

        var progress = await new GoalProgressService(_db, new GoalRealizedReader(_db)).GetProgressAsync(Guid.NewGuid(), goal.Id);

        progress.Should().BeNull();
    }
}
