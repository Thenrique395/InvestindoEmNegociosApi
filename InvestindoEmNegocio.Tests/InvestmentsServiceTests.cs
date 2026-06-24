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
    public async Task ListPositionsAsync_Should_Use_Cache_On_Second_Call()
    {
        var userId = Guid.NewGuid();
        var positions = new List<InvestmentPosition>
        {
            new(userId, InvestmentType.ACOES, "PETR4", 10, 20, DateOnly.FromDateTime(DateTime.UtcNow), "B3", "Acoes", null)
        };

        var positionRepository = new Mock<IInvestmentPositionRepository>();
        positionRepository
            .Setup(x => x.ListByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(positions);

        var sut = BuildSut(positionRepository: positionRepository);

        var first = await sut.ListPositionsAsync(userId, CancellationToken.None);
        var second = await sut.ListPositionsAsync(userId, CancellationToken.None);

        first.Should().HaveCount(1);
        second.Should().HaveCount(1);
        positionRepository.Verify(x => x.ListByUserAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetGoalAsync_Should_Return_Null_When_Not_Found()
    {
        var goalRepository = new Mock<IInvestmentGoalRepository>();
        goalRepository
            .Setup(x => x.GetByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvestmentGoal?)null);

        var sut = BuildSut(goalRepository: goalRepository);

        var result = await sut.GetGoalAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpsertGoalAsync_Should_Create_When_Not_Exists()
    {
        var userId = Guid.NewGuid();
        var goalRepository = new Mock<IInvestmentGoalRepository>();
        goalRepository
            .Setup(x => x.GetByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvestmentGoal?)null);

        var sut = BuildSut(goalRepository: goalRepository);

        var result = await sut.UpsertGoalAsync(userId, new UpsertInvestmentGoalRequest(15000m), CancellationToken.None);

        result.TargetAmount.Should().Be(15000m);
        goalRepository.Verify(x => x.AddAsync(It.IsAny<InvestmentGoal>(), It.IsAny<CancellationToken>()), Times.Once);
        goalRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpsertGoalAsync_Should_Update_When_Exists()
    {
        var userId = Guid.NewGuid();
        var existing = new InvestmentGoal(userId, 5000m);
        var goalRepository = new Mock<IInvestmentGoalRepository>();
        goalRepository
            .Setup(x => x.GetByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var sut = BuildSut(goalRepository: goalRepository);

        var result = await sut.UpsertGoalAsync(userId, new UpsertInvestmentGoalRequest(9000m), CancellationToken.None);

        result.TargetAmount.Should().Be(9000m);
        goalRepository.Verify(x => x.AddAsync(It.IsAny<InvestmentGoal>(), It.IsAny<CancellationToken>()), Times.Never);
        goalRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

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
    public async Task UpsertAllocationTargetAsync_Should_Create_When_Not_Exists()
    {
        var userId = Guid.NewGuid();
        var allocationRepository = new Mock<IInvestmentAllocationTargetRepository>();
        allocationRepository
            .Setup(x => x.GetByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvestmentAllocationTarget?)null);

        var sut = BuildSut(allocationRepository: allocationRepository);

        var result = await sut.UpsertAllocationTargetAsync(
            userId,
            new UpsertInvestmentAllocationTargetRequest(40, 30, 20, 10),
            CancellationToken.None);

        result.Total.Should().Be(100m);
        allocationRepository.Verify(x => x.AddAsync(It.IsAny<InvestmentAllocationTarget>(), It.IsAny<CancellationToken>()), Times.Once);
        allocationRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpsertAllocationTargetAsync_Should_Update_When_Exists()
    {
        var userId = Guid.NewGuid();
        var current = new InvestmentAllocationTarget(userId, 25, 25, 25, 25);
        var allocationRepository = new Mock<IInvestmentAllocationTargetRepository>();
        allocationRepository
            .Setup(x => x.GetByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);

        var sut = BuildSut(allocationRepository: allocationRepository);

        var result = await sut.UpsertAllocationTargetAsync(
            userId,
            new UpsertInvestmentAllocationTargetRequest(50, 20, 20, 10),
            CancellationToken.None);

        result.Rf.Should().Be(50m);
        allocationRepository.Verify(x => x.AddAsync(It.IsAny<InvestmentAllocationTarget>(), It.IsAny<CancellationToken>()), Times.Never);
        allocationRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePositionAsync_Should_Create_And_Invalidate_Cache()
    {
        var userId = Guid.NewGuid();
        var positionRepository = new Mock<IInvestmentPositionRepository>();
        var sut = BuildSut(positionRepository: positionRepository);
        var request = new CreateInvestmentPositionRequest(
            InvestmentType.ACOES,
            "PETR4",
            10,
            22.5m,
            DateOnly.FromDateTime(DateTime.UtcNow),
            "B3",
            "Acoes",
            null);

        var created = await sut.CreatePositionAsync(userId, request, CancellationToken.None);

        created.Asset.Should().Be("PETR4");
        positionRepository.Verify(x => x.AddAsync(It.IsAny<InvestmentPosition>(), It.IsAny<CancellationToken>()), Times.Once);
        positionRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdatePositionAsync_Should_Return_Null_When_Position_Does_Not_Exist()
    {
        var userId = Guid.NewGuid();
        var positionRepository = new Mock<IInvestmentPositionRepository>();
        positionRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvestmentPosition?)null);
        var sut = BuildSut(positionRepository: positionRepository);

        var result = await sut.UpdatePositionAsync(
            userId,
            Guid.NewGuid(),
            new CreateInvestmentPositionRequest(InvestmentType.ACOES, "PETR4", 1, 10, DateOnly.FromDateTime(DateTime.UtcNow), string.Empty, string.Empty, string.Empty),
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdatePositionAsync_Should_Update_When_Position_Exists()
    {
        var userId = Guid.NewGuid();
        var position = new InvestmentPosition(userId, InvestmentType.ACOES, "PETR4", 1, 10, DateOnly.FromDateTime(DateTime.UtcNow), string.Empty, string.Empty, string.Empty);
        var positionRepository = new Mock<IInvestmentPositionRepository>();
        positionRepository
            .Setup(x => x.GetByIdAsync(position.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(position);
        var sut = BuildSut(positionRepository: positionRepository);

        var updated = await sut.UpdatePositionAsync(
            userId,
            position.Id,
            new CreateInvestmentPositionRequest(InvestmentType.ACOES, "PETR4", 3, 15, position.OpenedAt, "Conta", "Acoes", "nota"),
            CancellationToken.None);

        updated.Should().NotBeNull();
        updated!.Quantity.Should().Be(3);
        updated.AvgPrice.Should().Be(15);
        positionRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeletePositionAsync_Should_Return_False_When_Position_Not_Found()
    {
        var userId = Guid.NewGuid();
        var positionRepository = new Mock<IInvestmentPositionRepository>();
        positionRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvestmentPosition?)null);
        var sut = BuildSut(positionRepository: positionRepository);

        var deleted = await sut.DeletePositionAsync(userId, Guid.NewGuid(), CancellationToken.None);

        deleted.Should().BeFalse();
    }

    [Fact]
    public async Task DeletePositionAsync_Should_MarkDeleted_And_Return_True_When_Position_Exists()
    {
        var userId = Guid.NewGuid();
        var position = new InvestmentPosition(userId, InvestmentType.ACOES, "PETR4", 2, 10, DateOnly.FromDateTime(DateTime.UtcNow), string.Empty, string.Empty, string.Empty);
        var positionRepository = new Mock<IInvestmentPositionRepository>();
        positionRepository
            .Setup(x => x.GetByIdAsync(position.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(position);
        var sut = BuildSut(positionRepository: positionRepository);

        var deleted = await sut.DeletePositionAsync(userId, position.Id, CancellationToken.None);

        deleted.Should().BeTrue();
        position.DeletedAt.Should().NotBeNull();
        positionRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeletePositionAsync_Should_MarkDeleted_On_Movements_Cascade()
    {
        var userId = Guid.NewGuid();
        var position = new InvestmentPosition(userId, InvestmentType.ACOES, "PETR4", 2, 10, DateOnly.FromDateTime(DateTime.UtcNow), string.Empty, string.Empty, string.Empty);
        var movement = new InvestmentMovement(position.Id, InvestmentMovementType.COMPRA, 1, 10, DateOnly.FromDateTime(DateTime.UtcNow));
        position.ApplyMovement(movement);

        var positionRepository = new Mock<IInvestmentPositionRepository>();
        positionRepository
            .Setup(x => x.GetByIdAsync(position.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(position);
        var sut = BuildSut(positionRepository: positionRepository);

        await sut.DeletePositionAsync(userId, position.Id, CancellationToken.None);

        movement.DeletedAt.Should().NotBeNull();
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

    private static IInvestmentsService BuildSut(
        Mock<IInvestmentGoalRepository>? goalRepository = null,
        Mock<IInvestmentAllocationTargetRepository>? allocationRepository = null,
        Mock<IInvestmentPositionRepository>? positionRepository = null,
        Mock<IMarketDataService>? marketDataService = null)
    {
        var planningService = new InvestmentPlanningService(
            goalRepository?.Object ?? Mock.Of<IInvestmentGoalRepository>(),
            allocationRepository?.Object ?? Mock.Of<IInvestmentAllocationTargetRepository>(),
            NullLogger<InvestmentPlanningService>.Instance);
        var positionService = new InvestmentPositionService(
            positionRepository?.Object ?? Mock.Of<IInvestmentPositionRepository>(),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<InvestmentPositionService>.Instance);
        var marketEnrichmentService = new InvestmentMarketEnrichmentService(
            marketDataService?.Object ?? Mock.Of<IMarketDataService>(),
            NullLogger<InvestmentMarketEnrichmentService>.Instance);

        return new InvestmentsService(
            planningService,
            planningService,
            planningService,
            planningService,
            positionService,
            positionService,
            marketEnrichmentService);
    }
}
