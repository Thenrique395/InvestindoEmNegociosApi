using System.Text;
using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace InvestindoEmNegocio.Tests;

public class DataPortabilityServiceTests
{
    [Fact]
    public async Task ExportAsync_Should_Use_Cache_When_Enabled()
    {
        await using var dbContext = CreateDbContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new DataPortabilityOptions { ExportCacheSeconds = 60 });
        var sut = new DataPortabilityService(dbContext, cache, options);

        var userId = Guid.NewGuid();

        var first = await sut.ExportAsync(userId, CancellationToken.None);
        var second = await sut.ExportAsync(userId, CancellationToken.None);

        first.FileName.Should().Be(second.FileName);
        first.Content.Should().Equal(second.Content);
        Encoding.UTF8.GetString(first.Content).Should().Contain(userId.ToString(), Exactly.Once());
    }

    [Fact]
    public async Task ImportAsync_Should_Return_Zero_For_Empty_Snapshot()
    {
        await using var dbContext = CreateDbContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new DataPortabilityOptions { ExportCacheSeconds = 0 });
        var sut = new DataPortabilityService(dbContext, cache, options);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

        var result = await sut.ImportAsync(Guid.NewGuid(), stream, replaceExisting: false, CancellationToken.None);

        result.ImportedRecords.Should().Be(0);
    }

    [Fact]
    public async Task ImportAsync_Should_Import_Complete_Snapshot()
    {
        await using var dbContext = CreateDbContext();
        await EnsureReferenceDataAsync(dbContext);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new DataPortabilityOptions { ExportCacheSeconds = 0 });
        var sut = new DataPortabilityService(dbContext, cache, options);
        var userId = Guid.NewGuid();

        var snapshot = BuildCompleteSnapshot(userId);
        await using var stream = SerializeSnapshot(snapshot);

        var result = await sut.ImportAsync(userId, stream, replaceExisting: false, CancellationToken.None);

        result.ImportedRecords.Should().Be(14);
        (await dbContext.UserProfiles.CountAsync(x => x.UserId == userId)).Should().Be(1);
        (await dbContext.Categories.CountAsync(x => x.UserId == userId)).Should().Be(1);
        (await dbContext.Cards.CountAsync(x => x.UserId == userId)).Should().Be(1);
        (await dbContext.Goals.CountAsync(x => x.UserId == userId)).Should().Be(1);
        (await dbContext.GoalContributions.CountAsync(x => x.UserId == userId)).Should().Be(1);
        (await dbContext.MoneyPlans.CountAsync(x => x.UserId == userId)).Should().Be(1);
        (await dbContext.MoneyInstallments.CountAsync(x => x.UserId == userId)).Should().Be(1);
        (await dbContext.MoneyPayments.CountAsync(x => x.UserId == userId)).Should().Be(1);
        (await dbContext.InvestmentGoals.CountAsync(x => x.UserId == userId)).Should().Be(1);
        (await dbContext.InvestmentAllocationTargets.CountAsync(x => x.UserId == userId)).Should().Be(1);
        (await dbContext.InvestmentPositions.CountAsync(x => x.UserId == userId)).Should().Be(1);
        (await dbContext.InvestmentMovements.CountAsync()).Should().Be(1);
        (await dbContext.UserOnboardings.CountAsync(x => x.UserId == userId)).Should().Be(1);
        (await dbContext.UserNotifications.CountAsync(x => x.UserId == userId)).Should().Be(1);
    }

    [Fact]
    public async Task ImportAsync_Should_Replace_Existing_Data_When_Requested()
    {
        await using var dbContext = CreateDbContext();
        await EnsureReferenceDataAsync(dbContext);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new DataPortabilityOptions { ExportCacheSeconds = 0 });
        var sut = new DataPortabilityService(dbContext, cache, options);
        var userId = Guid.NewGuid();

        await SeedExistingUserDataAsync(dbContext, userId);

        var snapshot = BuildCompleteSnapshot(userId);
        await using var stream = SerializeSnapshot(snapshot);

        var result = await sut.ImportAsync(userId, stream, replaceExisting: true, CancellationToken.None);

        result.ImportedRecords.Should().Be(14);
        var categories = await dbContext.Categories.Where(x => x.UserId == userId).ToListAsync();
        categories.Should().ContainSingle();
        categories[0].Name.Should().Be("Nova Categoria");

        var cards = await dbContext.Cards.Where(x => x.UserId == userId).ToListAsync();
        cards.Should().ContainSingle();
        cards[0].Nickname.Should().Be("Novo Cartao");

        var notifications = await dbContext.UserNotifications.Where(x => x.UserId == userId).ToListAsync();
        notifications.Should().ContainSingle();
        notifications[0].ReferenceKey.Should().Be("new-notification-ref");
    }

    [Fact]
    public async Task ImportAsync_Should_Throw_When_Json_Is_Invalid()
    {
        await using var dbContext = CreateDbContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new DataPortabilityOptions { ExportCacheSeconds = 0 });
        var sut = new DataPortabilityService(dbContext, cache, options);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{ invalid json }"));

        Func<Task> act = async () => await sut.ImportAsync(Guid.NewGuid(), stream, replaceExisting: false, CancellationToken.None);

        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task ImportAsync_Should_Throw_When_Required_Field_Is_Missing()
    {
        await using var dbContext = CreateDbContext();
        await EnsureReferenceDataAsync(dbContext);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new DataPortabilityOptions { ExportCacheSeconds = 0 });
        var sut = new DataPortabilityService(dbContext, cache, options);
        var userId = Guid.NewGuid();

        var snapshot = new UserDataSnapshot
        {
            SourceUserId = userId,
            Profile = new UserProfileData(
                Guid.NewGuid(),
                "   ",
                "12345678900",
                "+55 11 999999999",
                null,
                "",
                "Sao Paulo",
                "SP",
                "Brasil",
                "vida-financeira",
                1,
                "B",
                "pt-BR",
                "BRL",
                true,
                true,
                false,
                true,
                3,
                DateTime.UtcNow.AddDays(-1),
                DateTime.UtcNow)
        };
        await using var stream = SerializeSnapshot(snapshot);

        Func<Task> act = async () => await sut.ImportAsync(userId, stream, replaceExisting: false, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*campo obrigatório ausente*profile.fullName*");
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

    private static MemoryStream SerializeSnapshot(UserDataSnapshot snapshot)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(snapshot);
        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }

    private static UserDataSnapshot BuildCompleteSnapshot(Guid sourceUserId)
    {
        var categoryId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var installmentId = Guid.NewGuid();
        var positionId = Guid.NewGuid();

        return new UserDataSnapshot
        {
            SourceUserId = sourceUserId,
            Profile = new UserProfileData(
                Guid.NewGuid(),
                "Usuario Novo",
                "12345678900",
                "+55 11 999999999",
                new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                "",
                "Sao Paulo",
                "SP",
                "Brasil",
                "vida-financeira",
                1,
                "B",
                "pt-BR",
                "BRL",
                true,
                true,
                false,
                true,
                3,
                DateTime.UtcNow.AddDays(-10),
                DateTime.UtcNow),
            Categories =
            [
                new CategoryData(categoryId, "Nova Categoria", MoneyType.Expense, true, DateTime.UtcNow)
            ],
            Cards =
            [
                new CardData(cardId, 1, "Banco X", 5000m, 10, 20, "Usuario Novo", "Novo Cartao", "1234", DateTime.UtcNow, DateTime.UtcNow)
            ],
            Goals =
            [
                new GoalData(goalId, "Meta 2026", 10000m, 1000m, 2026, 500m, null, null, GoalStatus.InProgress, DateTime.UtcNow, DateTime.UtcNow)
            ],
            GoalContributions =
            [
                new GoalContributionData(Guid.NewGuid(), goalId, 200m, DateOnly.FromDateTime(DateTime.UtcNow), "Aporte", DateTime.UtcNow)
            ],
            Plans =
            [
                new MoneyPlanData(planId, MoneyType.Expense, "Plano novo", categoryId, cardId, 250m, ScheduleType.OneTime, null, 1, 1, DateOnly.FromDateTime(DateTime.UtcNow), PlanStatus.Active, DateTime.UtcNow, DateTime.UtcNow)
            ],
            Installments =
            [
                new MoneyInstallmentData(installmentId, planId, 1, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), null, 250m, InstallmentStatus.Open, DateTime.UtcNow, DateTime.UtcNow)
            ],
            Payments =
            [
                new MoneyPaymentData(Guid.NewGuid(), installmentId, DateTime.UtcNow, 250m, 1, "Pago", DateTime.UtcNow)
            ],
            InvestmentGoal = new InvestmentGoalData(Guid.NewGuid(), 150000m, DateTime.UtcNow, DateTime.UtcNow),
            InvestmentAllocationTarget = new InvestmentAllocationTargetData(Guid.NewGuid(), 40m, 30m, 20m, 10m, DateTime.UtcNow, DateTime.UtcNow),
            InvestmentPositions =
            [
                new InvestmentPositionData(positionId, InvestmentType.ACOES, "PETR4", 10m, 30m, DateOnly.FromDateTime(DateTime.UtcNow), "B3", "Ações", null, DateTime.UtcNow, DateTime.UtcNow)
            ],
            InvestmentMovements =
            [
                new InvestmentMovementData(Guid.NewGuid(), positionId, InvestmentMovementType.COMPRA, 10m, 30m, DateOnly.FromDateTime(DateTime.UtcNow), "Compra inicial", DateTime.UtcNow)
            ],
            Onboarding = new UserOnboardingData(Guid.NewGuid(), 3, true, DateTime.UtcNow, DateTime.UtcNow),
            Notifications =
            [
                new UserNotificationData(Guid.NewGuid(), planId, installmentId, MoneyType.Expense, NotificationKind.ExpenseUpcoming, "new-notification-ref", "Titulo", "Mensagem", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), DateTime.UtcNow, null)
            ]
        };
    }

    private static async Task SeedExistingUserDataAsync(InvestDbContext dbContext, Guid userId)
    {
        var category = new Category(userId, "Categoria Antiga", MoneyType.Expense);
        var card = new Card(userId, 1, "Usuario", "Cartao Antigo", "1111", "Banco Antigo", 1000m, 5, 15);
        var goal = new Goal(userId, "Meta Antiga", 5000m, 2025, status: GoalStatus.Planned, currentAmount: 0, expectedMonthly: 200m);
        var plan = new MoneyPlan(userId, MoneyType.Expense, "Plano Antigo", 100m, ScheduleType.OneTime, DateOnly.FromDateTime(DateTime.UtcNow), null, 1, 1, category.Id, card.Id);
        var installment = new MoneyInstallment(plan.Id, userId, 1, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)), 100m);
        var payment = new MoneyPayment(installment.Id, userId, DateTime.UtcNow, 100m, 1, "Pago");
        var profile = new UserProfile(userId, "Usuario Antigo", "11111111111", "11999999999", null);
        var onboarding = new UserOnboarding(userId, 1, false);
        var investmentGoal = new InvestmentGoal(userId, 50000m);
        var allocationTarget = new InvestmentAllocationTarget(userId, 25m, 25m, 25m, 25m);
        var position = new InvestmentPosition(userId, InvestmentType.RF, "TESOURO", 1m, 100m, DateOnly.FromDateTime(DateTime.UtcNow), "Conta", "RF");
        var movement = new InvestmentMovement(position.Id, InvestmentMovementType.APORTE, 1m, 100m, DateOnly.FromDateTime(DateTime.UtcNow), "Inicial");
        var contribution = new GoalContribution(goal.Id, userId, 50m, DateOnly.FromDateTime(DateTime.UtcNow), "Aporte");
        var notification = new UserNotification(userId, NotificationKind.ExpenseUpcoming, "Antigo", "Mensagem", "old-notification-ref");

        await dbContext.Categories.AddAsync(category);
        await dbContext.Cards.AddAsync(card);
        await dbContext.Goals.AddAsync(goal);
        await dbContext.MoneyPlans.AddAsync(plan);
        await dbContext.MoneyInstallments.AddAsync(installment);
        await dbContext.MoneyPayments.AddAsync(payment);
        await dbContext.UserProfiles.AddAsync(profile);
        await dbContext.UserOnboardings.AddAsync(onboarding);
        await dbContext.InvestmentGoals.AddAsync(investmentGoal);
        await dbContext.InvestmentAllocationTargets.AddAsync(allocationTarget);
        await dbContext.InvestmentPositions.AddAsync(position);
        await dbContext.InvestmentMovements.AddAsync(movement);
        await dbContext.GoalContributions.AddAsync(contribution);
        await dbContext.UserNotifications.AddAsync(notification);
        await dbContext.SaveChangesAsync();
    }

    private static async Task EnsureReferenceDataAsync(InvestDbContext dbContext)
    {
        if (!await dbContext.CardBrands.AnyAsync(x => x.Id == 1))
        {
            await dbContext.CardBrands.AddAsync(new CardBrand(1, "Visa", "visa"));
        }

        if (!await dbContext.PaymentMethods.AnyAsync(x => x.Id == 1))
        {
            await dbContext.PaymentMethods.AddAsync(new PaymentMethod(1, "PIX"));
        }

        await dbContext.SaveChangesAsync();
    }
}
