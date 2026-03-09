using FluentAssertions;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Moq;

namespace InvestindoEmNegocio.Tests;

[Trait("Suite", "Smoke")]
public class CashflowProjectionEngineTests
{
    [Fact]
    public async Task ProjectAsync_Should_Group_Overdue_Items_On_Reference_Day_And_Find_Risk_Date()
    {
        var userId = Guid.NewGuid();
        var today = new DateOnly(2026, 3, 9);
        var account = new Account(userId, "Conta", AccountType.Checking, 1000m);
        var accountId = Guid.NewGuid();
        typeof(Account).GetProperty(nameof(Account.Id))?.SetValue(account, accountId);

        var accountRepository = new Mock<IAccountRepository>();
        accountRepository.Setup(x => x.ListByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([account]);

        var transactionRepository = new Mock<IAccountTransactionRepository>();
        transactionRepository.Setup(x => x.SumSignedAmountByAccountAsync(accountId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository.Setup(x => x.ListByUserAsync(userId, null, null, It.IsAny<DateOnly?>(), MoneyType.Expense, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new MoneyInstallment(Guid.NewGuid(), userId, 1, today.AddDays(-2), 300m),
                new MoneyInstallment(Guid.NewGuid(), userId, 2, today.AddDays(1), 900m)
            ]);
        installmentRepository.Setup(x => x.ListByUserAsync(userId, InstallmentStatus.Open, null, It.IsAny<DateOnly?>(), MoneyType.Income, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new MoneyInstallment(Guid.NewGuid(), userId, 1, today.AddDays(3), 500m)
            ]);

        var sut = new CashflowProjectionEngine(accountRepository.Object, transactionRepository.Object, installmentRepository.Object);

        var result = await sut.ProjectAsync(userId, "month", today);

        result.OpeningBalance.Should().Be(1000m);
        result.RiskDate.Should().Be(today.AddDays(1));
        result.LowestBalance.Should().Be(-200m);
        result.LowestBalanceDate.Should().Be(today.AddDays(1));
        result.ProjectedClosingBalance.Should().Be(300m);
        result.Points[0].Date.Should().Be(today);
        result.Points[0].ExpensesAmount.Should().Be(300m);
        result.Points[1].Date.Should().Be(today.AddDays(1));
        result.Points[1].ClosingBalance.Should().Be(-200m);
        result.Points[3].IncomesAmount.Should().Be(500m);
    }
}
