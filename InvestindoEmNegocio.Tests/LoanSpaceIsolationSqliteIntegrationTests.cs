using FluentAssertions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Data;
using InvestindoEmNegocio.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Tests;

/// <summary>
/// Isolamento por área (BACKEND_PADROES_IMPLEMENTACAO.md, Multi-tenancy).
///
/// Antes do fix, as entidades de empréstimo tinham a coluna <c>SpaceId</c> mas NENHUM
/// repositório filtrava por ela: um contrato criado na área B aparecia na listagem da
/// área A. Estes testes existem para que isso não volte.
/// </summary>
public class LoanSpaceIsolationSqliteIntegrationTests
{
    private sealed class EspacoFixo(Guid? spaceId) : ICurrentSpaceAccessor
    {
        public Guid? SpaceId { get; } = spaceId;
        public Guid RequireSpaceId() => SpaceId ?? throw new UnauthorizedAccessException("Espaço ativo não encontrado.");
    }

    private static LoanContract NovoContrato(Guid userId, Guid spaceId, string titulo) =>
        new(userId, spaceId, titulo, 12000m, 18m, 0.015m,
            InterestRatePeriod.AnnualNominal, 12, LoanAmortizationType.Price,
            new DateOnly(2026, 7, 5), 15, 1100m, 13200m, 1200m, 13200m);

    private static DbContextOptions<InvestDbContext> Opcoes(SqliteConnection conn) =>
        new DbContextOptionsBuilder<InvestDbContext>().UseSqlite(conn).Options;

    [Fact]
    public async Task Contrato_De_Outra_Area_Nao_Aparece_Na_Listagem_Nem_Por_Id()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = Opcoes(connection);

        var userId = Guid.NewGuid();
        var areaPessoal = Guid.NewGuid();
        var areaNegocio = Guid.NewGuid();
        Guid idDoNegocio;

        await using (var db = new InvestDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            var pessoal = NovoContrato(userId, areaPessoal, "Financiamento pessoal");
            var negocio = NovoContrato(userId, areaNegocio, "Capital de giro");
            db.LoanContracts.AddRange(pessoal, negocio);
            await db.SaveChangesAsync();
            idDoNegocio = negocio.Id;
        }

        await using (var db = new InvestDbContext(options))
        {
            var repo = new LoanContractRepository(db, new EspacoFixo(areaPessoal));

            var visiveis = await repo.ListByUserAsync(userId);
            visiveis.Should().ContainSingle(c => c.Title == "Financiamento pessoal");

            // Mesmo usuário, mesmo id válido — mas de outra área.
            var vazamento = await repo.GetByIdAsync(idDoNegocio, userId);
            vazamento.Should().BeNull("contrato de outra área não pode ser carregado por id");
        }
    }

    [Fact]
    public async Task Parcela_Herda_O_Isolamento_Do_Contrato()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = Opcoes(connection);

        var userId = Guid.NewGuid();
        var areaPessoal = Guid.NewGuid();
        var areaNegocio = Guid.NewGuid();
        Guid parcelaDoNegocio;

        await using (var db = new InvestDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            var pessoal = NovoContrato(userId, areaPessoal, "Financiamento pessoal");
            var negocio = NovoContrato(userId, areaNegocio, "Capital de giro");
            db.LoanContracts.AddRange(pessoal, negocio);
            await db.SaveChangesAsync();

            var p1 = new LoanInstallment(pessoal.Id, userId, 1, new DateOnly(2026, 8, 15), 12000m, 900m, 200m, 1100m, 11000m);
            var p2 = new LoanInstallment(negocio.Id, userId, 1, new DateOnly(2026, 8, 15), 12000m, 900m, 200m, 1100m, 11000m);
            db.LoanInstallments.AddRange(p1, p2);
            await db.SaveChangesAsync();
            parcelaDoNegocio = p2.Id;
        }

        await using (var db = new InvestDbContext(options))
        {
            // LoanInstallment não tem coluna SpaceId: o isolamento vem do contrato pai.
            var repo = new LoanInstallmentRepository(db, new EspacoFixo(areaPessoal));

            var visiveis = await repo.ListByUserAsync(userId);
            visiveis.Should().HaveCount(1);

            var vazamento = await repo.GetByIdAsync(parcelaDoNegocio, userId);
            vazamento.Should().BeNull("parcela de contrato de outra área não pode ser carregada");
        }
    }

    [Fact]
    public async Task Sem_Area_Ativa_Nao_Filtra_Para_Nao_Quebrar_Job_Em_Segundo_Plano()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = Opcoes(connection);

        var userId = Guid.NewGuid();

        await using (var db = new InvestDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            db.LoanContracts.AddRange(
                NovoContrato(userId, Guid.NewGuid(), "Financiamento pessoal"),
                NovoContrato(userId, Guid.NewGuid(), "Capital de giro"));
            await db.SaveChangesAsync();
        }

        await using (var db = new InvestDbContext(options))
        {
            // Fora de requisição autenticada SpaceId é null — o contrato do accessor diz que
            // nesse caso não se filtra, senão robôs e jobs deixariam de enxergar dado.
            var repo = new LoanContractRepository(db, new EspacoFixo(null));
            var todos = await repo.ListByUserAsync(userId);
            todos.Should().HaveCount(2);
        }
    }
}
