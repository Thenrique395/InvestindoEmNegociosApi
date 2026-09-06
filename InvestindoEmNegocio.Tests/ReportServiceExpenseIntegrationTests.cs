using FluentAssertions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Data;
using InvestindoEmNegocio.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace InvestindoEmNegocio.Tests;

/// <summary>
/// A4 — Garante que o relatório mensal conta a despesa a partir do lançamento
/// (MoneyInstallment pago) UMA única vez, ignorando a movimentação de caixa
/// (AccountTransaction InstallmentPayment). Ou seja, pagar a parcela/fatura não
/// duplica a despesa.
/// </summary>
public class ReportServiceExpenseIntegrationTests
{
    [Fact]
    public async Task GetMonthlySummary_Should_Count_Paid_Expense_Once_Ignoring_Payment_Transaction()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InvestDbContext>().UseSqlite(connection).Options;

        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();

        await using (var db = new InvestDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();

            var account = new Account(userId, spaceId, "Conta", AccountType.Checking, 1000m);
            db.Accounts.Add(account);

            var plan = new MoneyPlan(userId, spaceId, MoneyType.Expense, "Cartão", 100m, ScheduleType.OneTime, new DateOnly(2026, 7, 5));
            db.MoneyPlans.Add(plan);

            var installment = new MoneyInstallment(plan.Id, userId, spaceId, 1, new DateOnly(2026, 7, 10), 100m);
            installment.RefreshPaymentStatus(100m); // marca como Paid
            db.MoneyInstallments.Add(installment);

            // Liquidação da fatura no caixa — NÃO deve entrar como despesa no relatório.
            db.AccountTransactions.Add(new AccountTransaction(
                account.Id, userId, spaceId, new DateTime(2026, 7, 10), AccountTransactionKind.Debit, 100m,
                "Pagamento da parcela", AccountTransactionSourceTypes.InstallmentPayment, installment.Id));

            await db.SaveChangesAsync();
        }

        await using (var db = new InvestDbContext(options))
        {
            var spaceAccessor = Mock.Of<ICurrentSpaceAccessor>();
            var service = new ReportService(
                new MoneyInstallmentRepository(db, spaceAccessor),
                new MoneyPlanRepository(db, spaceAccessor),
                new CategoryRepository(db));

            var report = await service.GetMonthlySummaryAsync(userId, 2026, 7);

            // Despesa contada uma única vez (100), não duplicada pelo pagamento (que somaria 200).
            report.TotalExpenses.Should().Be(100m);
        }
    }

    [Fact]
    public async Task GetMonthlySummary_Should_Include_Anticipated_Expense_In_Total()
    {
        // Antecipada é dinheiro que saiu de verdade, só que antes do vencimento.
        // O resumo somava apenas Paid e deixava essas de fora.
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InvestDbContext>().UseSqlite(connection).Options;

        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();

        await using (var db = new InvestDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();

            var plan = new MoneyPlan(userId, spaceId, MoneyType.Expense, "Internet", 100m, ScheduleType.OneTime, new DateOnly(2026, 7, 1));
            db.MoneyPlans.Add(plan);

            var paga = new MoneyInstallment(plan.Id, userId, spaceId, 1, new DateOnly(2026, 7, 10), 100m);
            paga.RefreshPaymentStatus(100m);
            db.MoneyInstallments.Add(paga);

            var antecipada = new MoneyInstallment(plan.Id, userId, spaceId, 2, new DateOnly(2026, 7, 20), 94.80m);
            antecipada.Anticipate(new DateOnly(2026, 7, 15), new DateOnly(2026, 6, 30));
            db.MoneyInstallments.Add(antecipada);

            var emAberto = new MoneyInstallment(plan.Id, userId, spaceId, 3, new DateOnly(2026, 7, 25), 50m);
            db.MoneyInstallments.Add(emAberto);

            await db.SaveChangesAsync();
        }

        await using (var db = new InvestDbContext(options))
        {
            var spaceAccessor = Mock.Of<ICurrentSpaceAccessor>();
            var service = new ReportService(
                new MoneyInstallmentRepository(db, spaceAccessor),
                new MoneyPlanRepository(db, spaceAccessor),
                new CategoryRepository(db));

            var report = await service.GetMonthlySummaryAsync(userId, 2026, 7);

            // 100 paga + 94,80 antecipada. A em aberto continua fora do realizado.
            report.TotalExpenses.Should().Be(194.80m);
        }
    }
}
