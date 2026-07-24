using FluentAssertions;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Tests;

/// <summary>
/// Regressão: uma meta de DESPESA nasce com Mode=Limit (DefaultModeFor(Expense)).
/// Antes, GoalConfiguration tinha HasDefaultValue(GoalMode.Target); como GoalMode.Limit
/// é o CLR default (0/sentinel), o EF tratava "Limit setado pelo app" como "não setado"
/// e aplicava o default do banco (Target) no insert — corrompendo silenciosamente o Mode.
/// Remover o HasDefaultValue faz o EF gravar sempre o valor real.
/// </summary>
public class GoalModePersistenceSqliteIntegrationTests
{
    [Theory]
    [InlineData(GoalKind.Expense, GoalMode.Limit)]
    [InlineData(GoalKind.Income, GoalMode.Target)]
    [InlineData(GoalKind.Investment, GoalMode.RecurringContribution)]
    public async Task Goal_Should_Persist_Default_Mode_For_Kind(GoalKind kind, GoalMode expectedMode)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InvestDbContext>().UseSqlite(connection).Options;

        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        Guid goalId;

        await using (var db = new InvestDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            // O construtor define Mode = DefaultModeFor(kind).
            var goal = new Goal(userId, spaceId, "Meta QA", 100m, 2026, null, GoalStatus.Planned, 0m, 10m, null, kind);
            db.Goals.Add(goal);
            await db.SaveChangesAsync();
            goalId = goal.Id;
        }

        await using (var db = new InvestDbContext(options))
        {
            var reloaded = await db.Goals.AsNoTracking().FirstAsync(g => g.Id == goalId);
            reloaded.Kind.Should().Be(kind);
            reloaded.Mode.Should().Be(expectedMode);
        }
    }
}
