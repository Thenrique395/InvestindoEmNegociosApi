using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Infrastructure.Data;
using InvestindoEmNegocio.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InvestindoEmNegocio.Tests;

public class RobotRunnerTests
{
    [Fact]
    public async Task RunSafelyByNameAsync_Should_Skip_When_Recent_Success_Exists()
    {
        await using var dbContext = CreateDbContext();
        dbContext.RobotExecutionLogs.Add(new RobotExecutionLog(
            "RoboLembretes",
            DateTime.UtcNow.AddMinutes(-2),
            DateTime.UtcNow.AddMinutes(-1),
            100,
            "corr-prev",
            "host-prev",
            null,
            success: true,
            processedCount: 3));
        await dbContext.SaveChangesAsync();

        var runner = new RobotRunner([new FakeRobotTask()], dbContext, NullLogger<RobotRunner>.Instance);
        var result = await runner.RunSafelyByNameAsync("RoboLembretes", cooldownMinutes: 10, triggeredByUserId: null, CancellationToken.None);

        result.Should().NotBeNull();
        result!.WasSkipped.Should().BeTrue();
        result.SkipReason.Should().Contain("RECENT_SUCCESSFUL_RUN");

        var last = await dbContext.RobotExecutionLogs.OrderByDescending(x => x.StartedAt).FirstAsync();
        last.WasSkipped.Should().BeTrue();
        last.ZeroItemsReasonCode.Should().Be("SKIPPED_SAFETY_WINDOW");
    }

    private static InvestDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InvestDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var dbContext = new InvestDbContext(options);
        dbContext.Database.OpenConnection();
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private sealed class FakeRobotTask : IRobotTask
    {
        public string Name => "RoboLembretes";

        public Task<RobotTaskExecutionResult> RunAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new RobotTaskExecutionResult(1, 1, 1, 0));
    }
}
