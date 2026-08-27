using FluentAssertions;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Tests;

/// <summary>
/// O endpoint de estorno devolvia 500 em DEV e em PRD, para QUALQUER pagamento — não era
/// dado específico. A causa era a constraint <c>ck_payment_amount_positive</c> exigindo
/// <c>PaidAmount &gt; 0</c>, enquanto o estorno é gravado como pagamento NEGATIVO
/// espelhando o original. Schema e código se contradiziam, e o estorno nunca funcionou.
///
/// Este teste guarda o schema: pagamento negativo precisa persistir; zero, não.
/// </summary>
public class ReversePaymentSqliteIntegrationTests
{
    private static DbContextOptions<InvestDbContext> Opcoes(SqliteConnection c) =>
        new DbContextOptionsBuilder<InvestDbContext>().UseSqlite(c).Options;

    [Fact]
    public async Task Estorno_Persiste_Como_Pagamento_Negativo()
    {
        await using var conn = new SqliteConnection("DataSource=:memory:");
        await conn.OpenAsync();
        var options = Opcoes(conn);

        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();

        await using var db = new InvestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var plan = new MoneyPlan(userId, spaceId, MoneyType.Income, "Salário", 1000m,
            ScheduleType.Recurring, new DateOnly(2026, 8, 1), FrequencyType.Monthly);
        db.MoneyPlans.Add(plan);
        await db.SaveChangesAsync();

        var inst = new MoneyInstallment(plan.Id, userId, spaceId, 1, new DateOnly(2026, 8, 1), 1000m);
        db.MoneyInstallments.Add(inst);
        await db.SaveChangesAsync();

        db.MoneyPayments.Add(new MoneyPayment(inst.Id, userId, spaceId, DateTime.UtcNow, 1000m, null, null, null));
        await db.SaveChangesAsync();

        // O estorno: espelho negativo do pagamento original.
        db.MoneyPayments.Add(new MoneyPayment(inst.Id, userId, spaceId, DateTime.UtcNow, -1000m, null, "Estorno", null));
        var act = async () => await db.SaveChangesAsync();

        await act.Should().NotThrowAsync("estorno é pagamento negativo — a constraint não pode barrar");
        db.MoneyPayments.Count(p => p.PaidAmount < 0).Should().Be(1);
    }

    [Fact]
    public async Task Pagamento_Zero_Continua_Barrado()
    {
        await using var conn = new SqliteConnection("DataSource=:memory:");
        await conn.OpenAsync();
        var options = Opcoes(conn);

        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();

        await using var db = new InvestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var plan = new MoneyPlan(userId, spaceId, MoneyType.Expense, "Conta", 100m,
            ScheduleType.OneTime, new DateOnly(2026, 8, 1));
        db.MoneyPlans.Add(plan);
        await db.SaveChangesAsync();

        var inst = new MoneyInstallment(plan.Id, userId, spaceId, 1, new DateOnly(2026, 8, 1), 100m);
        db.MoneyInstallments.Add(inst);
        await db.SaveChangesAsync();

        // Valor zero não significa nada — a constraint continua existindo para isso.
        db.MoneyPayments.Add(new MoneyPayment(inst.Id, userId, spaceId, DateTime.UtcNow, 0m, null, null, null));
        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
