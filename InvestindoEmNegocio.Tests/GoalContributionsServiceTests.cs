using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class GoalContributionsServiceTests
{
    [Fact]
    public async Task ListAsync_Should_Return_Null_When_Goal_Does_Not_Exist()
    {
        var goalRepository = new Mock<IGoalRepository>();
        goalRepository
            .Setup(x => x.ExistsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = BuildSut(goalRepository: goalRepository);

        var result = await sut.ListAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_Amount_Is_Invalid()
    {
        var sut = BuildSut();

        Func<Task> act = async () => await sut.CreateAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new GoalContributionRequest(0, DateOnly.FromDateTime(DateTime.UtcNow), null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_Should_Create_Contribution_And_Complete_Goal_When_Target_Reached()
    {
        var userId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var goal = new Goal(userId, "Reserva", 1000, 2026, currentAmount: 900, status: GoalStatus.InProgress);

        var goalRepository = new Mock<IGoalRepository>();
        goalRepository
            .Setup(x => x.GetByIdAsync(goalId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(goal);

        var contributionRepository = new Mock<IGoalContributionRepository>();

        var sut = BuildSut(goalRepository: goalRepository, goalContributionRepository: contributionRepository);

        var result = await sut.CreateAsync(
            userId,
            goalId,
            new GoalContributionRequest(100, DateOnly.FromDateTime(DateTime.UtcNow), "Aporte"),
            CancellationToken.None);

        result.Should().NotBeNull();
        goal.CurrentAmount.Should().Be(1000);
        goal.Status.Should().Be(GoalStatus.Completed);
        contributionRepository.Verify(x => x.AddAsync(It.IsAny<GoalContribution>(), It.IsAny<CancellationToken>()), Times.Once);
        contributionRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static GoalContributionsService BuildSut(
        Mock<IGoalRepository>? goalRepository = null,
        Mock<IGoalContributionRepository>? goalContributionRepository = null)
    {
        return new GoalContributionsService(
            goalRepository?.Object ?? Mock.Of<IGoalRepository>(),
            goalContributionRepository?.Object ?? Mock.Of<IGoalContributionRepository>(),
            NullLogger<GoalContributionsService>.Instance);
    }
}
