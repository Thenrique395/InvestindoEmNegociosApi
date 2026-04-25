using FluentAssertions;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Data;
using InvestindoEmNegocio.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Tests;

public class RepositoryCoverageTests
{
    [Fact]
    public async Task NotificationSettingsRepository_Should_GetOrCreate_And_Get()
    {
        await using var db = CreateDbContext();
        var repo = new NotificationSettingsRepository(db);

        var created = await repo.GetOrCreateAsync();
        var fetched = await repo.GetAsync();

        created.Should().NotBeNull();
        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(created.Id);

        var explicitSettings = new NotificationSettings(
            true, 1, true, 2, true, true, 3, true, true, true, true, true, true, 15);

        await repo.AddAsync(explicitSettings);
        await repo.SaveChangesAsync();

        var any = await db.NotificationSettings.AsNoTracking().ToListAsync();
        any.Should().HaveCount(2);
    }

    [Fact]
    public async Task MoneyInstallmentRepository_Should_Filter_List_And_Sum_CardDebt()
    {
        await using var db = CreateDbContext();
        var repo = new MoneyInstallmentRepository(db);

        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var brand = new CardBrand(1, "Visa", "visa");
        var card = new Card(userId, 1, "Nome", "Nick", "1234", "Banco", 1000, 10, 20);

        var expensePlan = new MoneyPlan(userId, MoneyType.Expense, "Despesa Cartao", 100, ScheduleType.Installments, new DateOnly(2026, 1, 1), null, 2, null, null, card.Id);
        var incomePlan = new MoneyPlan(userId, MoneyType.Income, "Receita", 500, ScheduleType.OneTime, new DateOnly(2026, 1, 1), null, 1, null, null, null);
        var foreignPlan = new MoneyPlan(otherUserId, MoneyType.Expense, "Outro", 70, ScheduleType.OneTime, new DateOnly(2026, 1, 1), null, 1, null, null, card.Id);

        await db.CardBrands.AddAsync(brand);
        await db.Cards.AddAsync(card);
        await db.MoneyPlans.AddRangeAsync(expensePlan, incomePlan, foreignPlan);

        var i1 = new MoneyInstallment(expensePlan.Id, userId, 1, new DateOnly(2026, 2, 10), 120);
        var i2 = new MoneyInstallment(expensePlan.Id, userId, 2, new DateOnly(2026, 3, 10), 80);
        var i3 = new MoneyInstallment(incomePlan.Id, userId, 1, new DateOnly(2026, 2, 15), 500);
        var i4 = new MoneyInstallment(foreignPlan.Id, otherUserId, 1, new DateOnly(2026, 2, 10), 70);
        i2.RestoreStatus(InstallmentStatus.Paid);

        await db.MoneyInstallments.AddRangeAsync(i1, i2, i3, i4);
        await db.SaveChangesAsync();

        var listByUser = await repo.ListByUserAsync(userId, null, null, null, null);
        listByUser.Should().HaveCount(3);

        var listByType = await repo.ListByUserAsync(userId, null, null, null, MoneyType.Expense);
        listByType.Should().HaveCount(2);

        var listByStatus = await repo.ListByUserAsync(userId, InstallmentStatus.Open, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28), MoneyType.Expense);
        listByStatus.Should().ContainSingle();

        var byPlan = await repo.ListByPlanAsync(expensePlan.Id, userId);
        byPlan.Should().HaveCount(2);

        var cardDebt = await repo.SumCardDebtAsync(userId);
        cardDebt.Should().Be(120m);

        var byId = await repo.GetByIdAsync(i1.Id);
        byId.Should().NotBeNull();

        repo.Remove(i1);
        repo.RemoveRange([i3]);
        await repo.SaveChangesAsync();

        var remaining = await db.MoneyInstallments.AsNoTracking().Where(x => x.UserId == userId).ToListAsync();
        remaining.Should().HaveCount(1);

        await repo.AddAsync(new MoneyInstallment(expensePlan.Id, userId, 3, new DateOnly(2026, 4, 10), 50));
        await repo.AddRangeAsync([new MoneyInstallment(expensePlan.Id, userId, 4, new DateOnly(2026, 5, 10), 60)]);
        await repo.SaveChangesAsync();

        var afterAdd = await db.MoneyInstallments.AsNoTracking().Where(x => x.UserId == userId).ToListAsync();
        afterAdd.Should().HaveCount(3);
    }

    private static InvestDbContext CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<InvestDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new InvestDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
