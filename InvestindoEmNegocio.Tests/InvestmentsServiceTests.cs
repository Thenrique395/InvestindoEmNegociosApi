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

    [Fact]
    public async Task AddMovementAsync_Should_Recalculate_AvgPrice_On_COMPRA()
    {
        var userId = Guid.NewGuid();
        var position = new InvestmentPosition(
            userId,
            InvestmentType.ACOES,
            "PETR4",
            10,
            20,
            DateOnly.FromDateTime(DateTime.UtcNow),
            "B3",
            "Ações",
            null);

        var positionRepository = new Mock<IInvestmentPositionRepository>();
        positionRepository
            .Setup(x => x.GetByIdAsync(position.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(position);

        var sut = BuildSut(positionRepository: positionRepository);

        var movement = await sut.AddMovementAsync(
            userId,
            position.Id,
            new CreateInvestmentMovementRequest(
                InvestmentMovementType.COMPRA,
                5,
                40,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Compra teste"),
            CancellationToken.None);

        movement.Type.Should().Be(InvestmentMovementType.COMPRA);
        position.Quantity.Should().Be(15);
        position.AvgPrice.Should().BeApproximately(26.666666m, 0.0001m);
        positionRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddMovementAsync_Should_Decrease_Quantity_On_VENDA()
    {
        var userId = Guid.NewGuid();
        var position = new InvestmentPosition(
            userId,
            InvestmentType.ACOES,
            "VALE3",
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

        await sut.AddMovementAsync(
            userId,
            position.Id,
            new CreateInvestmentMovementRequest(
                InvestmentMovementType.VENDA,
                3,
                31,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Venda teste"),
            CancellationToken.None);

        position.Quantity.Should().Be(7);
        position.AvgPrice.Should().Be(30);
        positionRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnrichWithMarketAsync_Should_Return_Original_When_Market_Service_Throws()
    {
        var items = new List<InvestmentPositionDto>
        {
            new(Guid.NewGuid(), InvestmentType.ACOES, "PETR4", 10, 20, DateOnly.FromDateTime(DateTime.UtcNow), "B3", "Ações", null, [])
        };

        var marketDataService = new Mock<IMarketDataService>();
        marketDataService
            .Setup(x => x.GetSnapshotsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("market offline"));

        var sut = BuildSut(marketDataService: marketDataService);

        var result = await sut.EnrichWithMarketAsync(items, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].MarketSymbol.Should().BeNull();
        result[0].Asset.Should().Be("PETR4");
    }

    [Fact]
    public async Task EnrichWithMarketAsync_Should_Map_Snapshot_Fields_When_Available()
    {
        var items = new List<InvestmentPositionDto>
        {
            new(Guid.NewGuid(), InvestmentType.ACOES, "PETR4 - Petrobras", 10, 20, DateOnly.FromDateTime(DateTime.UtcNow), "B3", "Ações", null, [])
        };

        var snapshots = new Dictionary<string, MarketSnapshotResponse>(StringComparer.OrdinalIgnoreCase)
        {
            ["PETR4"] = new(
                "PETR4",
                33.15m,
                1.25m,
                "BRL",
                "Petrobras",
                "https://logo",
                DateTimeOffset.UtcNow,
                "b3",
                false,
                "brapi")
        };

        var marketDataService = new Mock<IMarketDataService>();
        marketDataService
            .Setup(x => x.GetSnapshotsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshots);

        var sut = BuildSut(marketDataService: marketDataService);

        var result = await sut.EnrichWithMarketAsync(items, CancellationToken.None);

        result[0].MarketSymbol.Should().Be("PETR4");
        result[0].MarketPrice.Should().Be(33.15m);
        result[0].MarketChangePercent.Should().Be(1.25m);
        result[0].MarketName.Should().Be("Petrobras");
        result[0].MarketProvider.Should().Be("brapi");
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
