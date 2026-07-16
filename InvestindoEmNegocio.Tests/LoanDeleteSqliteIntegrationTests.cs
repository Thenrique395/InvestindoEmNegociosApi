using FluentAssertions;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Data;
using InvestindoEmNegocio.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Tests;

/// <summary>
/// A23 — Excluir um contrato de empréstimo deve funcionar e a cascata deve apagar as
/// parcelas, sem <see cref="DbUpdateConcurrencyException"/>. Regressão do 500 que
/// ocorria porque o delete explícito das parcelas (com Version = concurrency token)
/// competia com a FK ON DELETE CASCADE do banco.
/// </summary>
public class LoanDeleteSqliteIntegrationTests
{
    [Fact]
    public async Task DeleteLoan_Should_Cascade_Delete_Installments_Without_Error()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InvestDbContext>().UseSqlite(connection).Options;

        var userId = Guid.NewGuid();
        Guid contractId;

        await using (var db = new InvestDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();

            var contract = new LoanContract(userId, "Empréstimo QA", 12000m, 18m, 12,
                LoanAmortizationType.Price, new DateOnly(2026, 7, 5), 15, 1100m, 13200m, 1200m);
            db.LoanContracts.Add(contract);
            await db.SaveChangesAsync();
            contractId = contract.Id;

            for (var i = 1; i <= 3; i++)
            {
                db.LoanInstallments.Add(new LoanInstallment(
                    contractId, userId, i, new DateOnly(2026, 7 + i, 15),
                    12000m, 900m, 200m, 1100m, 11000m));
            }
            await db.SaveChangesAsync();
        }

        await using (var db = new InvestDbContext(options))
        {
            var service = new LoansService(new LoanContractRepository(db), new LoanInstallmentRepository(db));
            // Antes do fix, isto lançava DbUpdateConcurrencyException → 500.
            await service.DeleteAsync(userId, contractId);
        }

        await using (var db = new InvestDbContext(options))
        {
            (await db.LoanContracts.FindAsync(contractId)).Should().BeNull("o contrato foi excluído");
            (await db.LoanInstallments.CountAsync(x => x.ContractId == contractId))
                .Should().Be(0, "a cascata (ON DELETE CASCADE) deve apagar as parcelas");
        }
    }
}
