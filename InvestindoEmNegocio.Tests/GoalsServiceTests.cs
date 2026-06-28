using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class GoalsServiceTests
{
    [Fact]
    public async Task UpsertIncomeGoalAsync_Should_Throw_When_ExpectedMonthly_Is_Invalid()
    {
        var sut = new GoalsService(Mock.Of<IGoalRepository>(), Mock.Of<IGoalContributionRepository>(), Mock.Of<ICurrentSpaceAccessor>(), NullLogger<GoalsService>.Instance);

        Func<Task> act = async () => await sut.UpsertIncomeGoalAsync(Guid.NewGuid(), new UpsertIncomeGoalRequest(2026, 0));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*maior que zero*");
    }

    [Fact]
    public async Task UpsertIncomeGoalAsync_Should_Create_Goal_When_Not_Exists()
    {
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var repository = new Mock<IGoalRepository>();
        repository
            .Setup(x => x.ListByUserAsync(userId, 2026, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Goal>());

        var spaceAccessor = new Mock<ICurrentSpaceAccessor>();
        spaceAccessor.Setup(x => x.SpaceId).Returns(spaceId);
        spaceAccessor.Setup(x => x.RequireSpaceId()).Returns(spaceId);

        var sut = new GoalsService(repository.Object, Mock.Of<IGoalContributionRepository>(), spaceAccessor.Object, NullLogger<GoalsService>.Instance);

        var result = await sut.UpsertIncomeGoalAsync(userId, new UpsertIncomeGoalRequest(2026, 1000));

        result.Title.Should().Be("Meta de Receita");
        result.TargetAmount.Should().Be(12000);
        repository.Verify(x => x.AddAsync(It.IsAny<Goal>(), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_Should_Return_False_When_Goal_Not_Found()
    {
        var repository = new Mock<IGoalRepository>();
        repository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Goal?)null);
        var sut = new GoalsService(repository.Object, Mock.Of<IGoalContributionRepository>(), Mock.Of<ICurrentSpaceAccessor>(), NullLogger<GoalsService>.Instance);

        var removed = await sut.DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        removed.Should().BeFalse();
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_Should_MarkDeleted_On_Goal_And_Cascade_To_Contributions()
    {
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var goal = new Goal(userId, spaceId, "Viagem", 5000, 2026);
        var contribution = new GoalContribution(goal.Id, userId, spaceId, 200, DateOnly.FromDateTime(DateTime.UtcNow));

        var repository = new Mock<IGoalRepository>();
        repository.Setup(x => x.GetByIdAsync(goal.Id, userId, It.IsAny<CancellationToken>())).ReturnsAsync(goal);

        var contributionRepository = new Mock<IGoalContributionRepository>();
        contributionRepository
            .Setup(x => x.ListByGoalAsync(goal.Id, userId, It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync([contribution]);

        var sut = new GoalsService(repository.Object, contributionRepository.Object, Mock.Of<ICurrentSpaceAccessor>(), NullLogger<GoalsService>.Instance);

        var removed = await sut.DeleteAsync(userId, goal.Id);

        removed.Should().BeTrue();
        goal.DeletedAt.Should().NotBeNull();
        contribution.DeletedAt.Should().NotBeNull();
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
