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
            true,
            12));
        await dbContext.SaveChangesAsync();

        var robotTasks = new IRobotTask[]
        {
            new FakeRobotTask("ReminderRobot"),
            new FakeRobotTask("AnotherRobot")
        };
        var runner = new Mock<IRobotRunner>();
        var sut = new AdminRobotsService(robotTasks, runner.Object, dbContext);

        var result = await sut.MonitorAsync(50, CancellationToken.None);

        result.Robots.Should().HaveCount(2);
        result.Robots.Should().Contain(x => x.RobotName == "ReminderRobot" && x.LastSuccess == true && x.LastProcessedCount == 12);
        result.Robots.Should().Contain(x => x.RobotName == "AnotherRobot" && x.LastSuccess == null);
        result.RecentRuns.Should().HaveCount(1);
        result.RecentRuns[0].RobotName.Should().Be("ReminderRobot");
    }

    [Fact]
    public async Task RunAsync_Should_Delegate_To_Runner()
    {
        await using var dbContext = CreateDbContext();
        var expected = new RobotRunResultDto("ReminderRobot", DateTime.UtcNow, DateTime.UtcNow.AddSeconds(1), true, 3, null);
        var runner = new Mock<IRobotRunner>();
        runner
            .Setup(x => x.RunByNameAsync("ReminderRobot", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var sut = new AdminRobotsService([new FakeRobotTask("ReminderRobot")], runner.Object, dbContext);
        var result = await sut.RunAsync("ReminderRobot", CancellationToken.None);

        result.Should().NotBeNull();
        result!.RobotName.Should().Be("ReminderRobot");
        runner.Verify(x => x.RunByNameAsync("ReminderRobot", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAllAsync_Should_Delegate_To_Runner()
    {
        await using var dbContext = CreateDbContext();
        var expected = new List<RobotRunResultDto>
        {
            new("ReminderRobot", DateTime.UtcNow, DateTime.UtcNow.AddSeconds(1), true, 1, null)
        };

        var runner = new Mock<IRobotRunner>();
        runner
            .Setup(x => x.RunAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var sut = new AdminRobotsService([new FakeRobotTask("ReminderRobot")], runner.Object, dbContext);
        var result = await sut.RunAllAsync(CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].RobotName.Should().Be("ReminderRobot");
        runner.Verify(x => x.RunAllAsync(It.IsAny<CancellationToken>()), Times.Once);
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

    private sealed class FakeRobotTask(string name) : IRobotTask
    {
        public string Name { get; } = name;
        public Task<int> RunAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}
