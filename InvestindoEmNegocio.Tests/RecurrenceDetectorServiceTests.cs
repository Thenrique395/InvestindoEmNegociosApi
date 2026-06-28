using FluentAssertions;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Tests;

public class RecurrenceDetectorServiceTests
{
    [Fact]
    public async Task SuggestAsync_Should_Return_High_Score_When_Recurring_Plan_Already_Exists()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        await dbContext.MoneyPlans.AddAsync(new MoneyPlan(
            userId,
            spaceId,
            MoneyType.Expense,
            "Academia Premium",
            89.90m,
            ScheduleType.Recurring,
            new DateOnly(2026, 1, 5),
            FrequencyType.Monthly));
        await dbContext.SaveChangesAsync();

        var sut = new RecurrenceDetectorService(dbContext);

        var result = await sut.SuggestAsync(userId, MoneyType.Expense, "Academia Premium 03/2026", 89.90m, DateTime.UtcNow, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Frequency.Should().Be("Monthly");
        result.Score.Should().Be(98);
        result.ConfidenceBand.Should().Be("high");
        result.ReasonCode.Should().Be("existing_recurring_plan");
    }

    [Fact]
    public async Task SuggestAsync_Should_Return_Medium_Score_When_Monthly_Transaction_Pattern_Is_Detected()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var account = new Account(userId, spaceId, "Conta", AccountType.Checking, 0m);
        await dbContext.Accounts.AddAsync(account);
        var transactions = new[]
        {
            new AccountTransaction(account.Id, userId, spaceId, new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), AccountTransactionKind.Debit, 32.90m, "Spotify 01/2026", "BankStatementImport", Guid.NewGuid()),
            new AccountTransaction(account.Id, userId, spaceId, new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc), AccountTransactionKind.Debit, 32.90m, "Spotify 02/2026", "BankStatementImport", Guid.NewGuid()),
            new AccountTransaction(account.Id, userId, spaceId, new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc), AccountTransactionKind.Debit, 32.90m, "Spotify 03/2026", "BankStatementImport", Guid.NewGuid())
        };
        await dbContext.AccountTransactions.AddRangeAsync(transactions);
        await dbContext.SaveChangesAsync();

        var sut = new RecurrenceDetectorService(dbContext);

        var result = await sut.SuggestAsync(userId, MoneyType.Expense, "Spotify 04/2026", 32.90m, new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Frequency.Should().Be("Monthly");
        result.Score.Should().Be(93);
        result.ConfidenceBand.Should().Be("medium");
        result.ReasonCode.Should().Be("monthly_transaction_pattern");
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
