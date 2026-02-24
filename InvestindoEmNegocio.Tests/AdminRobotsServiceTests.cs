using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class AdminRobotsServiceTests
{
    [Fact]
    public async Task MonitorAsync_Should_Return_Status_And_Recent_Runs()
    {
        await using var dbContext = CreateDbContext();
        var startedAt = DateTime.UtcNow.AddMinutes(-5);
        var finishedAt = DateTime.UtcNow.AddMinutes(-4);

        dbContext.RobotExecutionLogs.Add(new RobotExecutionLog(
            "ReminderRobot",
            startedAt,
            finishedAt,
            durationMs: 100,
            correlationId: "corr-1",
            hostName: "host-1",
            triggeredByUserId: null,
            success: true,
            processedCount: 12,
            emailsAttempted: 2,
            emailsSent: 2,
            emailsFailed: 0,
            zeroItemsReasonCode: null,
            wasSkipped: false,
            skipReason: null,
            error: null));
        await dbContext.SaveChangesAsync();

        var robotTasks = new IRobotTask[]
        {
            new FakeRobotTask("ReminderRobot"),
            new FakeRobotTask("AnotherRobot")
        };
        var runner = new Mock<IRobotRunner>();
        var sut = new AdminRobotsService(robotTasks, runner.Object, dbContext);

        var result = await sut.MonitorAsync(new RobotMonitorQueryDto(50), CancellationToken.None);

        result.Summary24h.TotalRuns.Should().Be(1);
        result.Summary24h.ItemsGenerated.Should().Be(12);
        result.Summary24h.EmailsSent.Should().Be(2);
        result.Robots.Should().HaveCount(2);
        result.Robots.Should().Contain(x => x.RobotName == "ReminderRobot" && x.LastSuccess == true && x.LastProcessedCount == 12);
        result.RecentRuns.Should().HaveCount(1);
        result.RecentRuns[0].CorrelationId.Should().Be("corr-1");
    }

    [Fact]
    public async Task RunAsync_Should_Delegate_To_Runner_With_Safe_Mode_When_Force_Is_False()
    {
        await using var dbContext = CreateDbContext();
        var expected = BuildRunResult();
        var runner = new Mock<IRobotRunner>();
        runner
            .Setup(x => x.RunSafelyByNameAsync("ReminderRobot", 15, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var sut = new AdminRobotsService([new FakeRobotTask("ReminderRobot")], runner.Object, dbContext);
        var result = await sut.RunAsync("ReminderRobot", force: false, cooldownMinutes: 15, null, CancellationToken.None);

        result.Should().NotBeNull();
        runner.Verify(x => x.RunSafelyByNameAsync("ReminderRobot", 15, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_Should_Delegate_To_Runner_With_Force_Mode_When_Force_Is_True()
    {
        await using var dbContext = CreateDbContext();
        var expected = BuildRunResult();
        var runner = new Mock<IRobotRunner>();
        runner
            .Setup(x => x.RunByNameAsync("ReminderRobot", It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var sut = new AdminRobotsService([new FakeRobotTask("ReminderRobot")], runner.Object, dbContext);
        var result = await sut.RunAsync("ReminderRobot", force: true, cooldownMinutes: 15, null, CancellationToken.None);

        result.Should().NotBeNull();
        runner.Verify(x => x.RunByNameAsync("ReminderRobot", It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static RobotRunResultDto BuildRunResult() => new(
        "ReminderRobot",
        DateTime.UtcNow,
        DateTime.UtcNow.AddSeconds(1),
        1000,
        "corr",
        "host",
        null,
        true,
        3,
        new RobotExecutionMetricsDto(3, 1, 1, 0, null),
        false,
        null,
        null);

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

    private sealed class FakeRobotTask(string name) : IRobotTask
    {
        public string Name { get; } = name;
        public Task<RobotTaskExecutionResult> RunAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new RobotTaskExecutionResult(0));
    }
}
