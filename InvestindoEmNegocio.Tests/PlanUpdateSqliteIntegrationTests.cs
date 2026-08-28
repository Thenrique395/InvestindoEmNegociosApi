using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Data;
using InvestindoEmNegocio.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InvestindoEmNegocio.Tests;

/// <summary>
/// Editar a recorrência inteira (PUT /plans/{id}) devolvia 500 quando o plano tinha parcela
/// já paga. Caso real: mover o salário de 01/10 para 30/09 pela opção "toda a recorrência".
/// </summary>
public class PlanUpdateSqliteIntegrationTests
{
    private sealed class EspacoFixo(Guid id) : ICurrentSpaceAccessor
    {
        public Guid? SpaceId => id;
        public Guid RequireSpaceId() => id;
    }

    [Fact]
    public async Task UpdatePlan_Com_Parcela_Paga_Nao_Deve_Estourar()
    {
        await using var conn = new SqliteConnection("DataSource=:memory:");
        await conn.OpenAsync();
        var options = new DbContextOptionsBuilder<InvestDbContext>().UseSqlite(conn).Options;

        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        Guid planId;

        await using (var db = new InvestDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();

            var conta = new Account(userId, spaceId, "Conta principal", AccountType.Checking, 0m);
            db.Accounts.Add(conta);

            var plan = new MoneyPlan(userId, spaceId, MoneyType.Income, "Salário", 12700m,
                ScheduleType.Recurring, new DateOnly(2026, 7, 1), FrequencyType.Monthly);
            db.MoneyPlans.Add(plan);
            await db.SaveChangesAsync();
            planId = plan.Id;

            var paga = new MoneyInstallment(plan.Id, userId, spaceId, 2, new DateOnly(2026, 7, 31), 12263m);
            paga.RefreshPaymentStatus(12263m);
            db.MoneyInstallments.Add(paga);
            // #3..#6 em aberto — a regeneração antes reusava justamente esses números.
            foreach (var (no, mes) in new[] { (3, 8), (4, 10), (5, 11), (6, 12) })
                db.MoneyInstallments.Add(new MoneyInstallment(plan.Id, userId, spaceId, no, new DateOnly(2026, mes, 1), 12700m));
            await db.SaveChangesAsync();

            var pag = new MoneyPayment(paga.Id, userId, spaceId, new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc), 12263m, null, null, conta.Id);
            db.MoneyPayments.Add(pag);
            await db.SaveChangesAsync();

            db.AccountTransactions.Add(new AccountTransaction(conta.Id, userId, spaceId,
                new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc), AccountTransactionKind.Credit, 12263m,
                "Pagamento parcela 2 - Salário", AccountTransactionSourceTypes.InstallmentPayment, pag.Id));

            // Estorno: é o que a parcela do PRD tem a mais. O CleanupLedgerFromPaymentsAsync
            // só apaga transações de InstallmentPayment — a de estorno sobrevive.
            var estorno = new MoneyPayment(paga.Id, userId, spaceId,
                new DateTime(2026, 8, 27, 0, 0, 0, DateTimeKind.Utc), -12700m, null, "Estorno", conta.Id);
            db.MoneyPayments.Add(estorno);
            db.AccountTransactions.Add(new AccountTransaction(conta.Id, userId, spaceId,
                new DateTime(2026, 8, 27, 0, 0, 0, DateTimeKind.Utc), AccountTransactionKind.Debit, 12700m,
                "Estorno pagamento parcela 2 - Salário",
                AccountTransactionSourceTypes.InstallmentPaymentReversal, pag.Id));
            await db.SaveChangesAsync();
        }

        await using (var db = new InvestDbContext(options))
        {
            var acessor = new EspacoFixo(spaceId);
            var sut = new PlansService(
                new MoneyPlanRepository(db, acessor),
                new MoneyInstallmentRepository(db, acessor),
                new MoneyPaymentRepository(db, acessor),
                new AccountTransactionRepository(db, acessor),
                new CardRepository(db, acessor),
                new CategoryRepository(db),
                acessor,
                Mock.Of<IPlanHistoryService>(),
                NullLogger<PlansService>.Instance);

            var req = new CreatePlanRequest(MoneyType.Income, "Salário", 12700m, ScheduleType.Recurring,
                new DateOnly(2026, 9, 30), FrequencyType.Monthly, null, null, null, null);

            var act = async () => await sut.UpdateAsync(userId, planId, req);
            await act.Should().NotThrowAsync("mover a recorrência é operação suportada");
        }

        await using (var db = new InvestDbContext(options))
        {
            var todas = db.MoneyInstallments.Where(i => i.PlanId == planId).ToList();

            todas.Should().Contain(i => i.InstallmentNo == 2 && i.Status == InstallmentStatus.Paid,
                "parcela paga é preservada — editar a recorrência não pode apagar o passado");
            todas.Where(i => i.DueDate == new DateOnly(2026, 7, 31)).Should().HaveCount(1);

            db.MoneyPayments.Count(p => p.PaidAmount == 12263m).Should().Be(1,
                "o pagamento da parcela preservada continua existindo");

            todas.Where(i => i.Status == InstallmentStatus.Open)
                 .Should().OnlyContain(i => i.InstallmentNo > 2,
                     "as novas continuam a numeração — reusar número existente viola o índice único");
            todas.Select(i => i.InstallmentNo).Should().OnlyHaveUniqueItems();
        }
    }
}
