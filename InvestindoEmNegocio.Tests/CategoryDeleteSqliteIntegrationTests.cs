using FluentAssertions;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Data;
using InvestindoEmNegocio.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InvestindoEmNegocio.Tests;

/// <summary>
/// A6 — Excluir uma categoria EM USO não apaga os lançamentos. A FK
/// money_plans.CategoryId é ON DELETE SET NULL, então o MoneyPlan é preservado e
/// apenas fica "sem categoria". Este teste trava a regra contra uma troca
/// acidental da FK para CASCADE (que causaria perda de dados).
/// </summary>
public class CategoryDeleteSqliteIntegrationTests
{
    [Fact]
    public async Task DeleteCategory_InUse_Should_Null_MoneyPlan_Category_And_Preserve_Plan()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InvestDbContext>().UseSqlite(connection).Options;

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

        // Exclui a categoria via serviço, em um contexto novo (o plano não está
        // rastreado, então o SET NULL é aplicado pelo banco, não por fixup do EF).
        await using (var db = new InvestDbContext(options))
        {
            var service = new CategoriesService(new CategoryRepository(db), NullLogger<CategoriesService>.Instance);
            var removed = await service.DeleteAsync(userId, categoryId);
            removed.Should().BeTrue();
        }

        // Verifica: lançamento preservado e sem categoria; categoria removida.
        await using (var db = new InvestDbContext(options))
        {
            var plan = await db.MoneyPlans.FindAsync(planId);
            plan.Should().NotBeNull("o lançamento não pode ser apagado ao excluir a categoria");
            plan!.CategoryId.Should().BeNull("a FK money_plans.CategoryId é ON DELETE SET NULL");

            (await db.Categories.FindAsync(categoryId)).Should().BeNull("a categoria do usuário foi removida");
        }
    }
}
