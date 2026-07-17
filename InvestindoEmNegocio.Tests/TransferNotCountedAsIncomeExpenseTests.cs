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
/// A17 — Uma transferência entre contas NÃO pode ser contada como receita nem
/// despesa (evita dupla contagem), e as duas pontas se cancelam no saldo.
///
/// Usa o AccountTransferService REAL para criar a transferência (Debit na origem
/// + Credit no destino, SourceType=AccountTransfer) e valida via:
///  - ReportService (totais de receita/despesa vêm de MoneyInstallment, não de
///    AccountTransaction) → transferência não entra;
///  - SumSignedAmountByAccountAsync (a base do saldo real) → as pontas se cancelam;
///  - o tipo/source das transações criadas = AccountTransfer.
/// </summary>
public class TransferNotCountedAsIncomeExpenseTests
{
    [Fact]
    public async Task Transfer_Via_Service_Should_Not_Count_As_Income_Or_Expense_And_Net_To_Zero()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InvestDbContext>().UseSqlite(connection).Options;

        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var spaceAccessor = Mock.Of<ICurrentSpaceAccessor>();
        Guid fromId, toId;

        await using (var db = new InvestDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            var from = new Account(userId, spaceId, "Conta A", AccountType.Checking, 1000m);
            var to = new Account(userId, spaceId, "Conta B", AccountType.Checking, 500m);
            db.Accounts.Add(from);
            db.Accounts.Add(to);
            await db.SaveChangesAsync();
            fromId = from.Id;
            toId = to.Id;
        }

        // Transferência de R$ 300 A -> B, pelo SERVIÇO REAL.
        await using (var db = new InvestDbContext(options))
        {
            var service = new AccountTransferService(
                new AccountRepository(db, spaceAccessor),
                new AccountTransactionRepository(db, spaceAccessor),
                db,
                NullLogger<AccountTransferService>.Instance);

            var response = await service.TransferAsync(userId, new AccountTransferRequest(fromId, toId, 300m,
                new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc)));
            response.Should().NotBeNull();
        }

        // 1) Relatório mensal: transferência não é receita nem despesa.
        await using (var db = new InvestDbContext(options))
        {
            var report = new ReportService(
                new MoneyInstallmentRepository(db, spaceAccessor),
                new MoneyPlanRepository(db, spaceAccessor),
                new CategoryRepository(db));

            var summary = await report.GetMonthlySummaryAsync(userId, 2026, 7);
            summary.TotalIncome.Should().Be(0m, "transferência não é receita");
            summary.TotalExpenses.Should().Be(0m, "transferência não é despesa");
        }

        // 2) Saldo real (base): as pontas se cancelam; total = soma dos saldos iniciais.
        await using (var db = new InvestDbContext(options))
        {
            var accountRepo = new AccountRepository(db, spaceAccessor);
            var txRepo = new AccountTransactionRepository(db, spaceAccessor);
            var accounts = await accountRepo.ListByUserAsync(userId);

            decimal total = 0m;
            foreach (var acc in accounts)
                total += acc.InitialBalance + await txRepo.SumSignedAmountByAccountAsync(acc.Id, userId);

            total.Should().Be(1500m, "a transferência (-300 origem, +300 destino) não altera o saldo total");
        }

        // 3) O par de transações criado é do tipo transferência (não receita/despesa).
        await using (var db = new InvestDbContext(options))
        {
            var transfers = await db.AccountTransactions
                .Where(t => t.UserId == userId && t.SourceType == AccountTransactionSourceTypes.AccountTransfer)
                .ToListAsync();

            transfers.Should().HaveCount(2, "uma transferência gera débito na origem + crédito no destino");
            transfers.Count(t => t.Kind == AccountTransactionKind.Debit).Should().Be(1);
            transfers.Count(t => t.Kind == AccountTransactionKind.Credit).Should().Be(1);
        }
    }
}
