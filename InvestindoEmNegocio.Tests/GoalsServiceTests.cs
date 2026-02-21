using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
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
        var sut = new GoalsService(Mock.Of<IGoalRepository>(), NullLogger<GoalsService>.Instance);

        Func<Task> act = async () => await sut.UpsertIncomeGoalAsync(Guid.NewGuid(), new UpsertIncomeGoalRequest(2026, 0));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*maior que zero*");
    }

    [Fact]
    public async Task UpsertIncomeGoalAsync_Should_Create_Goal_When_Not_Exists()
    {
        var userId = Guid.NewGuid();
        var repository = new Mock<IGoalRepository>();
        repository
            .Setup(x => x.ListByUserAsync(userId, 2026, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Goal>());

        var sut = new GoalsService(repository.Object, NullLogger<GoalsService>.Instance);

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
        var sut = new GoalsService(repository.Object, NullLogger<GoalsService>.Instance);

        var removed = await sut.DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        removed.Should().BeFalse();
        repository.Verify(x => x.Remove(It.IsAny<Goal>()), Times.Never);
    }
}
