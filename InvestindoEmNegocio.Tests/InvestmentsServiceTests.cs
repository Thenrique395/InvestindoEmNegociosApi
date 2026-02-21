using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class InvestmentsServiceTests
{
    [Fact]
    public async Task GetAllocationTargetAsync_Should_Return_Default_When_Not_Configured()
    {
        var allocationRepository = new Mock<IInvestmentAllocationTargetRepository>();
        allocationRepository
            .Setup(x => x.GetByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvestmentAllocationTarget?)null);

        var sut = BuildSut(allocationRepository: allocationRepository);

        var result = await sut.GetAllocationTargetAsync(Guid.NewGuid(), CancellationToken.None);

        result.Rf.Should().Be(40);
        result.Acoes.Should().Be(35);
        result.Fundos.Should().Be(20);
        result.Cripto.Should().Be(5);
        result.Total.Should().Be(100);
    }

    [Fact]
    public async Task UpsertAllocationTargetAsync_Should_Throw_When_Total_Is_Not_100()
    {
        var sut = BuildSut();

        Func<Task> act = async () => await sut.UpsertAllocationTargetAsync(
            Guid.NewGuid(),
            new UpsertInvestmentAllocationTargetRequest(50, 20, 10, 10),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*precisa ser 100%*");
    }

    [Fact]
    public async Task AddMovementAsync_Should_Throw_When_Output_Quantity_Is_Greater_Than_Position()
    {
        var userId = Guid.NewGuid();
        var position = new InvestmentPosition(
            userId,
            InvestmentType.ACOES,
            "PETR4",
            10,
            30,
            DateOnly.FromDateTime(DateTime.UtcNow),
            "B3",
            "Ações",
            null);

        var positionRepository = new Mock<IInvestmentPositionRepository>();
        positionRepository
            .Setup(x => x.GetByIdAsync(position.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(position);

        var sut = BuildSut(positionRepository: positionRepository);

        Func<Task> act = async () => await sut.AddMovementAsync(
            userId,
            position.Id,
            new CreateInvestmentMovementRequest(
                InvestmentMovementType.VENDA,
                20,
                32,
                DateOnly.FromDateTime(DateTime.UtcNow),
                null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Quantidade de resgate maior que posição*");
    }

    private static InvestmentsService BuildSut(
        Mock<IInvestmentGoalRepository>? goalRepository = null,
        Mock<IInvestmentAllocationTargetRepository>? allocationRepository = null,
        Mock<IInvestmentPositionRepository>? positionRepository = null,
        Mock<IMarketDataService>? marketDataService = null)
    {
        return new InvestmentsService(
            goalRepository?.Object ?? Mock.Of<IInvestmentGoalRepository>(),
            allocationRepository?.Object ?? Mock.Of<IInvestmentAllocationTargetRepository>(),
            positionRepository?.Object ?? Mock.Of<IInvestmentPositionRepository>(),
            marketDataService?.Object ?? Mock.Of<IMarketDataService>(),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<InvestmentsService>.Instance);
    }
}
