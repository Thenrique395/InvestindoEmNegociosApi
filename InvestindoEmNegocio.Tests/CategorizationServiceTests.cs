using FluentAssertions;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class CategorizationServiceTests
{
    [Fact]
    public async Task SuggestAsync_Should_Use_History_When_User_Has_Previous_Category()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var category = new Category(userId, "Alimentação", MoneyType.Expense);
        var categoryId = category.Id;

        var categoryRepository = new Mock<ICategoryRepository>();
        categoryRepository
            .Setup(x => x.ListForUserAsync(userId, MoneyType.Expense, It.IsAny<CancellationToken>()))
            .ReturnsAsync([category]);

        var plan = new MoneyPlan(userId, Guid.NewGuid(), MoneyType.Expense, "Ifood Mercado", 100m, ScheduleType.OneTime, DateOnly.FromDateTime(DateTime.UtcNow), categoryId: categoryId);
        var planRepository = new Mock<IMoneyPlanRepository>();
        planRepository
            .Setup(x => x.ListByUserAsync(userId, MoneyType.Expense, It.IsAny<CancellationToken>()))
            .ReturnsAsync([plan]);

        var sut = new CategorizationService(categoryRepository.Object, planRepository.Object, dbContext, NullLogger<CategorizationService>.Instance);

        var result = await sut.SuggestAsync(userId, MoneyType.Expense, "Ifood Mercado", CancellationToken.None);

        result.Should().NotBeNull();
        result!.CategoryId.Should().Be(categoryId);
        result.ReasonCode.Should().Be("history_match");
        result.Score.Should().Be(98);
        result.ConfidenceBand.Should().Be("high");
    }

    [Fact]
    public async Task SuggestAsync_Should_Use_Merchant_Rule_When_History_Does_Not_Match()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var category = new Category(userId, "Transporte", MoneyType.Expense);
        var categoryId = category.Id;

        var categoryRepository = new Mock<ICategoryRepository>();
        categoryRepository
            .Setup(x => x.ListForUserAsync(userId, MoneyType.Expense, It.IsAny<CancellationToken>()))
            .ReturnsAsync([category]);

        var planRepository = new Mock<IMoneyPlanRepository>();
        planRepository
            .Setup(x => x.ListByUserAsync(userId, MoneyType.Expense, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = new CategorizationService(categoryRepository.Object, planRepository.Object, dbContext, NullLogger<CategorizationService>.Instance);

        var result = await sut.SuggestAsync(userId, MoneyType.Expense, "UBER TRIP 123", CancellationToken.None);

        result.Should().NotBeNull();
        result!.CategoryId.Should().Be(categoryId);
        result.ReasonCode.Should().Be("merchant_transport");
        result.Confidence.Should().Be(0.95m);
        result.Score.Should().Be(95);
        result.ConfidenceBand.Should().Be("high");
    }

    [Fact]
    public async Task SuggestAsync_Should_Use_Learned_Feedback_When_User_Corrects_Category()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var category = new Category(userId, "Transporte", MoneyType.Expense);
        var categoryId = category.Id;

        var categoryRepository = new Mock<ICategoryRepository>();
        categoryRepository
            .Setup(x => x.GetByIdForUserAsync(categoryId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        categoryRepository
            .Setup(x => x.ListDefaultsAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        categoryRepository
            .Setup(x => x.ListForUserAsync(userId, MoneyType.Expense, It.IsAny<CancellationToken>()))
            .ReturnsAsync([category]);

        var planRepository = new Mock<IMoneyPlanRepository>();
        planRepository
            .Setup(x => x.ListByUserAsync(userId, MoneyType.Expense, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = new CategorizationService(categoryRepository.Object, planRepository.Object, dbContext, NullLogger<CategorizationService>.Instance);

        await sut.LearnAsync(userId, MoneyType.Expense, "Uber Trip 123", categoryId, CancellationToken.None);
        var result = await sut.SuggestAsync(userId, MoneyType.Expense, "Uber Trip 999", CancellationToken.None);

        result.Should().NotBeNull();
        result!.CategoryId.Should().Be(categoryId);
        result.ReasonCode.Should().Be("learned_feedback");
        result.Confidence.Should().Be(0.99m);
        result.Score.Should().Be(99);
        result.ConfidenceBand.Should().Be("high");
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
}
