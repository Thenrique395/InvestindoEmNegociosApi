using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
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

[Trait("Suite", "Smoke")]
public class AccountsServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly InvestDbContext _dbContext;

    public AccountsServiceTests()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<InvestDbContext>().UseSqlite(_connection).Options;
        _dbContext = new InvestDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    private IInvestDbContext DbContext => _dbContext;

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task TransferAsync_Should_Throw_When_Amount_Is_Invalid()
    {
        var sut = BuildSut();
        var request = new AccountTransferRequest(Guid.NewGuid(), Guid.NewGuid(), 0);

        Func<Task> act = async () => await sut.TransferAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*maior que zero*");
    }

    [Fact]
    public async Task TransferAsync_Should_Return_Null_When_Any_Account_Not_Found()
    {
        var accountRepository = new Mock<IAccountRepository>();
        accountRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);
        var sut = BuildSut(accountRepository: accountRepository);

        var result = await sut.TransferAsync(Guid.NewGuid(), new AccountTransferRequest(Guid.NewGuid(), Guid.NewGuid(), 100));

        result.Should().BeNull();
    }

    [Fact]
    public async Task TransferAsync_Should_Create_Debit_And_Credit_With_Same_SourceId()
    {
        var userId = Guid.NewGuid();
        var from = new Account(userId, "Conta A", AccountType.Checking, 0);
        var to = new Account(userId, "Conta B", AccountType.Savings, 0);
        var fromId = from.Id;
        var toId = to.Id;

        var accountRepository = new Mock<IAccountRepository>();
        accountRepository.Setup(x => x.GetByIdAsync(fromId, userId, It.IsAny<CancellationToken>())).ReturnsAsync(from);
        accountRepository.Setup(x => x.GetByIdAsync(toId, userId, It.IsAny<CancellationToken>())).ReturnsAsync(to);

        var captured = new List<AccountTransaction>();
        var transactionRepository = new Mock<IAccountTransactionRepository>();
        transactionRepository
            .Setup(x => x.AddAsync(It.IsAny<AccountTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<AccountTransaction, CancellationToken>((tx, _) => captured.Add(tx))
            .Returns(Task.CompletedTask);

        var sut = BuildSut(accountRepository, transactionRepository);

        var result = await sut.TransferAsync(userId, new AccountTransferRequest(fromId, toId, 250m, DateTime.UtcNow, "Reserva"));

        result.Should().NotBeNull();
        captured.Should().HaveCount(2);
        captured.Count(x => x.Kind == AccountTransactionKind.Debit).Should().Be(1);
        captured.Count(x => x.Kind == AccountTransactionKind.Credit).Should().Be(1);
        captured.Select(x => x.SourceId).Distinct().Should().HaveCount(1);
        captured.All(x => x.SourceType == "AccountTransfer").Should().BeTrue();
        accountRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListTransactionsAsync_Should_Map_Transfer_Type()
    {
        var userId = Guid.NewGuid();
        var account = new Account(userId, "Conta", AccountType.Checking, 0);
        var accountId = account.Id;

        var accountRepository = new Mock<IAccountRepository>();
        accountRepository.Setup(x => x.GetByIdAsync(accountId, userId, It.IsAny<CancellationToken>())).ReturnsAsync(account);

        var transactionRepository = new Mock<IAccountTransactionRepository>();
        transactionRepository
            .Setup(x => x.ListByAccountAsync(accountId, userId, null, null, It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync([
                new AccountTransaction(accountId, userId, DateTime.UtcNow, AccountTransactionKind.Credit, 100m, "Transfer", "AccountTransfer", Guid.NewGuid())
            ]);

        var sut = BuildSut(accountRepository, transactionRepository);

        var items = await sut.ListTransactionsAsync(userId, accountId);

        items.Should().NotBeNull();
        items![0].Type.Should().Be(AccountTransactionType.Transfer);
    }

    private IAccountsService BuildSut(
        Mock<IAccountRepository>? accountRepository = null,
        Mock<IAccountTransactionRepository>? transactionRepository = null)
    {
        var query = new AccountQueryService(
            accountRepository?.Object ?? Mock.Of<IAccountRepository>(),
            transactionRepository?.Object ?? Mock.Of<IAccountTransactionRepository>());
        var command = new AccountCommandService(
            accountRepository?.Object ?? Mock.Of<IAccountRepository>(),
            transactionRepository?.Object ?? Mock.Of<IAccountTransactionRepository>(),
            NullLogger<AccountCommandService>.Instance);
        var transactionQuery = new AccountTransactionQueryService(
            accountRepository?.Object ?? Mock.Of<IAccountRepository>(),
            transactionRepository?.Object ?? Mock.Of<IAccountTransactionRepository>());
        var transfer = new AccountTransferService(
            accountRepository?.Object ?? Mock.Of<IAccountRepository>(),
            transactionRepository?.Object ?? Mock.Of<IAccountTransactionRepository>(),
            DbContext,
            NullLogger<AccountTransferService>.Instance);

        return new AccountsService(
            query,
            command,
            transactionQuery,
            transfer);
    }
}
