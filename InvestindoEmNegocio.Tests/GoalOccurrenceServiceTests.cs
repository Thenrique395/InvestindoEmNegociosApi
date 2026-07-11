using FluentAssertions;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using InvestindoEmNegocio.Infrastructure.Data;

namespace InvestindoEmNegocio.Tests;

[Trait("Suite", "Smoke")]
public class GoalOccurrenceServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly InvestDbContext _db;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _spaceId = Guid.NewGuid();
    private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.UtcNow);
    private readonly Dictionary<Guid, int> _seq = new();

    public GoalOccurrenceServiceTests()
    {
        _connection.Open();
        _db = new InvestDbContext(new DbContextOptionsBuilder<InvestDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
    }

    public void Dispose() { _db.Dispose(); _connection.Dispose(); }

    private DateOnly MonthStart(int monthsAgo = 0)
    {
        var d = new DateOnly(_today.Year, _today.Month, 1);
        return d.AddMonths(-monthsAgo);
    }

    private Guid AddCategory(MoneyType type)
    {
        var c = new Category(_userId, $"Cat-{Guid.NewGuid():N}", type);
        _db.Categories.Add(c);
        return c.Id;
    }

    private void AddPaidExpense(DateOnly due, decimal amount, Guid categoryId)
    {
        var plan = new MoneyPlan(_userId, _spaceId, MoneyType.Expense, "P", 100m, ScheduleType.OneTime, due, categoryId: categoryId);
        _db.MoneyPlans.Add(plan);
        var no = _seq.TryGetValue(plan.Id, out var cur) ? cur + 1 : 1;
        _seq[plan.Id] = no;
        var inst = new MoneyInstallment(plan.Id, _userId, _spaceId, no, due, amount);
        inst.RestoreStatus(InstallmentStatus.Paid);
        _db.MoneyInstallments.Add(inst);
    }

    private Goal AddGoal(RecurrenceType recurrence, DateOnly start, DateOnly end, decimal target = 1000m)
    {
        var goal = new Goal(_userId, _spaceId, "Meta", target, start.Year, kind: GoalKind.Expense);
        goal.ConfigurePlanning(GoalMode.Limit, start, end, recurrence, 80m, 100m);
        _db.Goals.Add(goal);
        return goal;
    }

    private GoalOccurrenceService Sut() => new(_db, new GoalRealizedReader(_db));

    [Fact]
    public async Task None_Recurrence_Has_Single_Occurrence_With_Realized()
    {
        var cat = AddCategory(MoneyType.Expense);
        var start = MonthStart();
        var end = start.AddMonths(1).AddDays(-1);
        var goal = AddGoal(RecurrenceType.None, start, end);
        AddPaidExpense(_today, 400m, cat);
        goal.ReplaceScopes(new[] { new GoalScope(goal.Id, GoalScopeType.Category, cat) });
        await _db.SaveChangesAsync();

        var list = await Sut().EnsureAndListAsync(_userId, goal.Id);

        list.Should().NotBeNull().And.HaveCount(1);
        list![0].Realized.Should().Be(400m);
        list[0].IsCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task Monthly_Recurrence_Generates_Occurrences_And_Closes_Past()
    {
        // Âncora 2 meses atrás -> 3 ocorrências (2 fechadas + a corrente).
        var start = MonthStart(2);
        var goal = AddGoal(RecurrenceType.Monthly, start, start.AddMonths(12));
        await _db.SaveChangesAsync();

        var list = await Sut().EnsureAndListAsync(_userId, goal.Id);

        list.Should().NotBeNull();
        list!.Should().HaveCount(3);
        list.Count(o => o.Status == nameof(GoalOccurrenceStatus.Closed)).Should().Be(2);
        list.Single(o => o.IsCurrent).Sequence.Should().Be(3);
    }

    [Fact]
    public async Task Override_Current_Occurrence_Changes_Only_Current_Target()
    {
        var start = MonthStart(1);
        var goal = AddGoal(RecurrenceType.Monthly, start, start.AddMonths(12), target: 1000m);
        await _db.SaveChangesAsync();

        var ok = await Sut().OverrideCurrentTargetAsync(_userId, goal.Id, 500m);
        ok.Should().BeTrue();

        var list = await Sut().EnsureAndListAsync(_userId, goal.Id);
        list!.Single(o => o.IsCurrent).TargetAmount.Should().Be(500m);
        list.Where(o => !o.IsCurrent).Should().OnlyContain(o => o.TargetAmount == 1000m);
    }

    [Fact]
    public async Task Returns_Null_For_Other_User()
    {
        var start = MonthStart();
        var goal = AddGoal(RecurrenceType.None, start, start.AddMonths(1).AddDays(-1));
        await _db.SaveChangesAsync();

        (await Sut().EnsureAndListAsync(Guid.NewGuid(), goal.Id)).Should().BeNull();
    }
}
