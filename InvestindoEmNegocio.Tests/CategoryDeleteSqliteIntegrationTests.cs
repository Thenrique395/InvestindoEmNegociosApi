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
/// A6 — Excluir uma categoria EM USO não a remove nem apaga os lançamentos: ela é
/// DESATIVADA (soft delete), preservando o vínculo do MoneyPlan com a categoria e o
/// histórico. Categorias sem uso são removidas fisicamente. Este teste trava a regra
/// híbrida contra regressões (perda de histórico ao excluir).
/// </summary>
public class CategoryDeleteSqliteIntegrationTests
{
    private static DbContextOptions<InvestDbContext> OptionsFor(SqliteConnection connection) =>
        new DbContextOptionsBuilder<InvestDbContext>().UseSqlite(connection).Options;

    private static CategoriesService BuildService(InvestDbContext db) =>
        new(new CategoryRepository(db),
            new MoneyPlanRepository(db, Mock.Of<ICurrentSpaceAccessor>()),
            NullLogger<CategoriesService>.Instance);

    [Fact]
    public async Task DeleteCategory_InUse_Should_Deactivate_And_Preserve_Plan_Link()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = OptionsFor(connection);

        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        Guid categoryId;
        Guid planId;

        await using (var db = new InvestDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();

            var category = new Category(userId, "Categoria QA", MoneyType.Expense);
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            categoryId = category.Id;

            var plan = new MoneyPlan(userId, spaceId, MoneyType.Expense, "Despesa vinculada", 100m,
                ScheduleType.OneTime, new DateOnly(2026, 7, 5), categoryId: categoryId);
            db.MoneyPlans.Add(plan);
            await db.SaveChangesAsync();
            planId = plan.Id;
        }

        await using (var db = new InvestDbContext(options))
        {
            var outcome = await BuildService(db).DeleteAsync(userId, categoryId);
            outcome.Should().Be(CategoryDeletionOutcome.Deactivated);
        }

        // Verifica: lançamento preservado COM a categoria; categoria mantida, porém inativa.
        await using (var db = new InvestDbContext(options))
        {
            var plan = await db.MoneyPlans.FindAsync(planId);
            plan.Should().NotBeNull("o lançamento não pode ser apagado ao excluir a categoria");
            plan!.CategoryId.Should().Be(categoryId, "categoria em uso é desativada, não removida — o vínculo do histórico é preservado");

            var category = await db.Categories.FindAsync(categoryId);
            category.Should().NotBeNull("a categoria em uso é mantida");
            category!.IsActive.Should().BeFalse("a categoria em uso é desativada");
        }
    }

    [Fact]
    public async Task DeleteCategory_NotInUse_Should_Hard_Delete()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = OptionsFor(connection);

        var userId = Guid.NewGuid();
        Guid categoryId;

        await using (var db = new InvestDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            var category = new Category(userId, "Nunca usada", MoneyType.Expense);
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            categoryId = category.Id;
        }

        await using (var db = new InvestDbContext(options))
        {
            var outcome = await BuildService(db).DeleteAsync(userId, categoryId);
            outcome.Should().Be(CategoryDeletionOutcome.Deleted);
        }

        await using (var db = new InvestDbContext(options))
        {
            (await db.Categories.FindAsync(categoryId)).Should().BeNull("categoria sem uso é removida fisicamente");
        }
    }
}
