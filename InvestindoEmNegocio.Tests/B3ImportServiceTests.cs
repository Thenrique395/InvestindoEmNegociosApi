using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;

namespace InvestindoEmNegocio.Tests;

public class B3ImportServiceTests
{
    [Fact]
    public async Task ConfirmAsync_Should_Throw_When_Token_Does_Not_Exist()
    {
        await using var dbContext = CreateDbContext();
        var sut = new B3ImportService(dbContext, new MemoryCache(new MemoryCacheOptions()), NullLogger<B3ImportService>.Instance);

        Func<Task> act = async () => await sut.ConfirmAsync(Guid.NewGuid(), new ConfirmB3ImportRequest("missing"), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Token expirado*");
    }

    [Fact]
    public async Task ImportSnapshotAsync_Should_Create_New_Position_When_Not_Exists()
    {
        await using var dbContext = CreateDbContext();
        var sut = new B3ImportService(dbContext, new MemoryCache(new MemoryCacheOptions()), NullLogger<B3ImportService>.Instance);

        var userId = Guid.NewGuid();
        var snapshot = new B3ImportSnapshot(
            "02/2026",
            "Holder",
            "00000000000",
            [new B3ExtractPosition("PETR4 - PETROBRAS", "ON", "BANCO TESTE S/A", 10m, 30m, 300m)],
            [],
            []);

        var result = await sut.ImportSnapshotAsync(userId, snapshot, "merge", CancellationToken.None);

        result.Imported.Should().Be(1);
        var positions = await dbContext.InvestmentPositions.Where(x => x.UserId == userId).ToListAsync();
        positions.Should().HaveCount(1);
        positions[0].Asset.Should().Be("PETR4");
        positions[0].Type.Should().Be(InvestmentType.ACOES);
    }

    [Fact]
    public async Task ConfirmAsync_Should_Throw_When_Token_Belongs_To_Another_User()
    {
        await using var dbContext = CreateDbContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new B3ImportService(dbContext, cache, NullLogger<B3ImportService>.Instance);

        var token = "foreign-token";
        var ownerUserId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();
        cache.Set(token, BuildCachedSnapshot(ownerUserId));

        Func<Task> act = async () => await sut.ConfirmAsync(
            anotherUserId,
            new ConfirmB3ImportRequest(token),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*outro usuário*");
    }

    [Fact]
    public async Task ImportSnapshotAsync_Should_Replace_Matching_Position_When_Strategy_Is_Replace()
    {
        await using var dbContext = CreateDbContext();
        var sut = new B3ImportService(dbContext, new MemoryCache(new MemoryCacheOptions()), NullLogger<B3ImportService>.Instance);
        var userId = Guid.NewGuid();

        var oldImported = new InvestmentPosition(
            userId,
            InvestmentType.ACOES,
            "PETR4",
            5m,
            20m,
            DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            "BANCO TESTE S/A",
            "B3");
        var unrelated = new InvestmentPosition(
            userId,
            InvestmentType.ACOES,
            "VALE3",
            3m,
            60m,
            DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            "OUTRO BANCO",
            "B3");

        await dbContext.InvestmentPositions.AddRangeAsync(oldImported, unrelated);
        await dbContext.SaveChangesAsync();

        var snapshot = new B3ImportSnapshot(
            "02/2026",
            "Holder",
            "00000000000",
            [new B3ExtractPosition("PETR4 - PETROBRAS", "ON", "BANCO TESTE S/A", 10m, 30m, 300m)],
            [],
            []);

        var result = await sut.ImportSnapshotAsync(userId, snapshot, "replace", CancellationToken.None);

        result.Imported.Should().Be(1);
        var positions = await dbContext.InvestmentPositions.Where(x => x.UserId == userId).ToListAsync();
        positions.Should().HaveCount(2);
        positions.Should().Contain(x => x.Asset == "VALE3");
        positions.Should().Contain(x => x.Asset == "PETR4" && x.Quantity == 10m && x.AvgPrice == 30m);
        positions.Should().NotContain(x => x.Id == oldImported.Id);
    }

    [Fact]
    public async Task ImportSnapshotAsync_Should_Not_Duplicate_Trade_Movement_When_Already_Exists()
    {
        await using var dbContext = CreateDbContext();
        var sut = new B3ImportService(dbContext, new MemoryCache(new MemoryCacheOptions()), NullLogger<B3ImportService>.Instance);
        var userId = Guid.NewGuid();

        var position = new InvestmentPosition(
            userId,
            InvestmentType.ACOES,
            "PETR4",
            10m,
            30m,
            DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            "BANCO TESTE S/A",
            "B3");

        var date = new DateOnly(2026, 2, 18);
        var existingMovement = new InvestindoEmNegocio.Domain.Entities.InvestmentMovement(
            position.Id,
            InvestmentMovementType.COMPRA,
            2m,
            31m,
            date,
            "Compra importada B3");
        position.ApplyMovement(existingMovement);

        await dbContext.InvestmentPositions.AddAsync(position);
        await dbContext.InvestmentMovements.AddAsync(existingMovement);
        await dbContext.SaveChangesAsync();

        var snapshot = new B3ImportSnapshot(
            "02/2026",
            "Holder",
            "00000000000",
            [],
            [],
            [new B3ExtractTrade("PETR4", "18/02/2026", "BANCO TESTE S/A", 2m, 0m, 2m, 31m, 0m)]);

        var result = await sut.ImportSnapshotAsync(userId, snapshot, "merge", CancellationToken.None);

        result.Imported.Should().Be(0);
        var movements = await dbContext.InvestmentMovements.Where(x => x.PositionId == position.Id).ToListAsync();
        movements.Should().HaveCount(1);
        movements[0].Type.Should().Be(InvestmentMovementType.COMPRA);
        movements[0].Quantity.Should().Be(2m);
    }

    private static InvestDbContext CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<InvestDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new InvestDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static object BuildCachedSnapshot(Guid userId)
    {
        var assembly = typeof(B3ImportService).Assembly;
        var parsedSnapshotType = assembly.GetType("InvestindoEmNegocio.Application.Services.B3ParsedSnapshot")
            ?? throw new InvalidOperationException("B3ParsedSnapshot type not found.");

        var parsedSnapshot = Activator.CreateInstance(
            parsedSnapshotType,
            args:
            [
                "02/2026",
                "Holder",
                "00000000000",
                new List<B3ExtractPosition>(),
                new List<B3ExtractIncome>(),
                new List<B3ExtractTrade>()
            ]) ?? throw new InvalidOperationException("Could not create parsed snapshot.");

        var cachedSnapshotType = typeof(B3ImportService).GetNestedType("CachedB3Snapshot", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CachedB3Snapshot type not found.");

        return Activator.CreateInstance(cachedSnapshotType, userId, parsedSnapshot)
            ?? throw new InvalidOperationException("Could not create cached snapshot.");
    }
}
