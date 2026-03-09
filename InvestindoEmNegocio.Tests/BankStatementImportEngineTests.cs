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

public class BankStatementImportEngineTests
{
    [Fact]
    public async Task ImportAsync_Should_Skip_Duplicate_Transactions_When_Enabled()
    {
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var account = new Account(userId, "Conta", AccountType.Checking, 0);
        typeof(Account).GetProperty(nameof(Account.Id))?.SetValue(account, accountId);

        var accountRepository = new Mock<IAccountRepository>();
        accountRepository.Setup(x => x.GetByIdAsync(accountId, userId, It.IsAny<CancellationToken>())).ReturnsAsync(account);

        var captured = new List<AccountTransaction>();
        var transactionRepository = new Mock<IAccountTransactionRepository>();
        transactionRepository
            .Setup(x => x.ListBySourceAsync(userId, "BankStatementImport", It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, string _, IEnumerable<Guid> ids, CancellationToken _) =>
            {
                var duplicate = ids.First();
                return [new AccountTransaction(accountId, userId, DateTime.UtcNow, AccountTransactionKind.Debit, 10m, "Existente", "BankStatementImport", duplicate)];
            });
        transactionRepository
            .Setup(x => x.AddAsync(It.IsAny<AccountTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<AccountTransaction, CancellationToken>((tx, _) => captured.Add(tx))
            .Returns(Task.CompletedTask);
        var categorizationService = new Mock<ICategorizationService>();
        categorizationService
            .Setup(x => x.SuggestAsync(It.IsAny<Guid>(), It.IsAny<MoneyType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CategorizationSuggestionDto?)null);
        categorizationService
            .Setup(x => x.LearnAsync(It.IsAny<Guid>(), It.IsAny<MoneyType>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var recurrenceDetectorService = new Mock<IRecurrenceDetectorService>();
        recurrenceDetectorService
            .Setup(x => x.SuggestAsync(It.IsAny<Guid>(), It.IsAny<MoneyType>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecurrenceSuggestionDto?)null);

        var sut = new BankStatementImportEngine(
            accountRepository.Object,
            transactionRepository.Object,
            new ImportIdentityEngine(),
            categorizationService.Object,
            recurrenceDetectorService.Object,
            NullLogger<BankStatementImportEngine>.Instance);

        var result = await sut.ImportAsync(userId, new BankStatementImportRequest(
            accountId,
            true,
            [
                new BankStatementImportItemDto("2026-03-01T00:00:00Z", 10m, AccountTransactionKind.Debit, "Padaria", null, "1", "DEBIT", null),
                new BankStatementImportItemDto("2026-03-02T00:00:00Z", 20m, AccountTransactionKind.Credit, "Salário", null, "2", "CREDIT", null)
            ]), CancellationToken.None);

        result.Created.Should().Be(1);
        result.Skipped.Should().Be(1);
        captured.Should().HaveCount(1);
        captured[0].Description.Should().Be("Salário");
    }
}
