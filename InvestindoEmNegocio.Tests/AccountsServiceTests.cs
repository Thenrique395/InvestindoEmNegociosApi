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

[Trait("Suite", "Smoke")]
public class AccountsServiceTests
{
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
        var fromId = Guid.NewGuid();
        var toId = Guid.NewGuid();
        var from = new Account(userId, "Conta A", AccountType.Checking, 0);
        var to = new Account(userId, "Conta B", AccountType.Savings, 0);
        typeof(Account).GetProperty(nameof(Account.Id))?.SetValue(from, fromId);
        typeof(Account).GetProperty(nameof(Account.Id))?.SetValue(to, toId);

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
        var accountId = Guid.NewGuid();
        var account = new Account(userId, "Conta", AccountType.Checking, 0);
        typeof(Account).GetProperty(nameof(Account.Id))?.SetValue(account, accountId);

        var accountRepository = new Mock<IAccountRepository>();
        accountRepository.Setup(x => x.GetByIdAsync(accountId, userId, It.IsAny<CancellationToken>())).ReturnsAsync(account);

        var transactionRepository = new Mock<IAccountTransactionRepository>();
        transactionRepository
            .Setup(x => x.ListByAccountAsync(accountId, userId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new AccountTransaction(accountId, userId, DateTime.UtcNow, AccountTransactionKind.Credit, 100m, "Transfer", "AccountTransfer", Guid.NewGuid())
            ]);

        var sut = BuildSut(accountRepository, transactionRepository);

        var items = await sut.ListTransactionsAsync(userId, accountId);

        items.Should().NotBeNull();
        items![0].Type.Should().Be(AccountTransactionType.Transfer);
    }

    [Fact]
    public async Task GetRealAvailableBalanceAsync_Should_Use_Active_Accounts_And_Open_Items()
    {
        var userId = Guid.NewGuid();
        var accountA = new Account(userId, "Conta A", AccountType.Checking, 1000m);
        var accountB = new Account(userId, "Conta B", AccountType.Savings, 500m);
        var accountInactive = new Account(userId, "Conta C", AccountType.Other, 999m);
        accountInactive.Deactivate();
        var accountAId = Guid.NewGuid();
        var accountBId = Guid.NewGuid();
        var accountInactiveId = Guid.NewGuid();
        typeof(Account).GetProperty(nameof(Account.Id))?.SetValue(accountA, accountAId);
        typeof(Account).GetProperty(nameof(Account.Id))?.SetValue(accountB, accountBId);
        typeof(Account).GetProperty(nameof(Account.Id))?.SetValue(accountInactive, accountInactiveId);

        var accountRepository = new Mock<IAccountRepository>();
        accountRepository.Setup(x => x.ListByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([accountA, accountB, accountInactive]);

        var transactionRepository = new Mock<IAccountTransactionRepository>();
        transactionRepository.Setup(x => x.SumSignedAmountByAccountAsync(accountAId, userId, It.IsAny<CancellationToken>())).ReturnsAsync(250m);
        transactionRepository.Setup(x => x.SumSignedAmountByAccountAsync(accountBId, userId, It.IsAny<CancellationToken>())).ReturnsAsync(-100m);

        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        installmentRepository.Setup(x => x.ListByUserAsync(userId, null, It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), MoneyType.Expense, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new MoneyInstallment(Guid.NewGuid(), userId, 1, today.AddDays(-1), 300m),
                new MoneyInstallment(Guid.NewGuid(), userId, 2, today.AddDays(2), 200m),
                BuildInstallment(userId, today.AddDays(10), 50m, InstallmentStatus.Anticipated)
            ]);
        installmentRepository.Setup(x => x.ListByUserAsync(userId, InstallmentStatus.Open, It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), MoneyType.Income, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new MoneyInstallment(Guid.NewGuid(), userId, 1, today.AddDays(3), 400m)
            ]);

        var sut = BuildSut(accountRepository, transactionRepository, installmentRepository);

        var result = await sut.GetRealAvailableBalanceAsync(userId, "month", today);

        result.ActiveAccountsBalance.Should().Be(1650m);
        result.PendingExpensesAmount.Should().Be(500m);
        result.PendingExpensesCount.Should().Be(2);
        result.PendingIncomesAmount.Should().Be(400m);
        result.RealAvailableBalance.Should().Be(1150m);
        result.ProjectedAvailableBalance.Should().Be(1550m);
        result.OverdueExpensesAmount.Should().Be(300m);
        result.OverdueExpensesCount.Should().Be(1);
        result.DueSoonExpensesAmount.Should().Be(200m);
    }

    [Fact]
    public async Task GetNetWorthSummaryAsync_Should_Compose_Assets_And_Liabilities_Without_Double_Counting_Cards()
    {
        var userId = Guid.NewGuid();
        var account = new Account(userId, "Conta principal", AccountType.Checking, 1000m);
        var accountId = Guid.NewGuid();
        typeof(Account).GetProperty(nameof(Account.Id))?.SetValue(account, accountId);

        var accountRepository = new Mock<IAccountRepository>();
        accountRepository.Setup(x => x.ListByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([account]);

        var transactionRepository = new Mock<IAccountTransactionRepository>();
        transactionRepository.Setup(x => x.SumSignedAmountByAccountAsync(accountId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(250m);

        var cardId = Guid.NewGuid();
        var planCard = new MoneyPlan(userId, MoneyType.Expense, "Fatura Visa", 400m, ScheduleType.OneTime, DateOnly.FromDateTime(DateTime.UtcNow), cardId: cardId);
        var planOther = new MoneyPlan(userId, MoneyType.Expense, "Curso", 200m, ScheduleType.OneTime, DateOnly.FromDateTime(DateTime.UtcNow));
        var planCardId = Guid.NewGuid();
        var planOtherId = Guid.NewGuid();
        typeof(MoneyPlan).GetProperty(nameof(MoneyPlan.Id))?.SetValue(planCard, planCardId);
        typeof(MoneyPlan).GetProperty(nameof(MoneyPlan.Id))?.SetValue(planOther, planOtherId);

        var card = new Card(userId, 1, "Henrique", "Visa Black", "1234", "Banco", 5000m, 10, 20);
        typeof(Card).GetProperty(nameof(Card.Id))?.SetValue(card, cardId);

        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        var cardInstallment = BuildInstallment(userId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5), 400m, InstallmentStatus.Open);
        var otherInstallment = BuildInstallment(userId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10), 200m, InstallmentStatus.PartiallyPaid);
        typeof(MoneyInstallment).GetProperty(nameof(MoneyInstallment.PlanId))?.SetValue(cardInstallment, planCardId);
        typeof(MoneyInstallment).GetProperty(nameof(MoneyInstallment.PlanId))?.SetValue(otherInstallment, planOtherId);
        installmentRepository.Setup(x => x.ListByUserAsync(userId, null, null, null, MoneyType.Expense, It.IsAny<CancellationToken>()))
            .ReturnsAsync([cardInstallment, otherInstallment]);

        var paymentRepository = new Mock<IMoneyPaymentRepository>();
        paymentRepository.Setup(x => x.ListByInstallmentIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new MoneyPayment(otherInstallment.Id, userId, DateTime.UtcNow, 50m)
            ]);

        var planRepository = new Mock<IMoneyPlanRepository>();
        planRepository.Setup(x => x.ListByUserAsync(userId, MoneyType.Expense, It.IsAny<CancellationToken>()))
            .ReturnsAsync([planCard, planOther]);

        var cardRepository = new Mock<ICardRepository>();
        cardRepository.Setup(x => x.ListByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([card]);

        var investmentsService = new Mock<IInvestmentsService>();
        var positions = new List<InvestmentPositionDto>
        {
            new(Guid.NewGuid(), InvestmentType.ACOES, "PETR4", 10m, 20m, DateOnly.FromDateTime(DateTime.UtcNow), "Broker", "Ações", null, [], "PETR4", 30m),
            new(Guid.NewGuid(), InvestmentType.RF, "Tesouro", 1m, 500m, DateOnly.FromDateTime(DateTime.UtcNow), "Conta", "Renda Fixa", null, []),
            new(Guid.NewGuid(), InvestmentType.IMOVEL, "Apartamento Boa Viagem", 1m, 350000m, DateOnly.FromDateTime(DateTime.UtcNow), "Patrimônio", "Imóvel", null, [])
        };
        investmentsService.Setup(x => x.ListPositionsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(positions);
        investmentsService.Setup(x => x.EnrichWithMarketAsync(It.IsAny<List<InvestmentPositionDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<InvestmentPositionDto> items, CancellationToken _) => items);

        var sut = BuildSut(
            accountRepository: accountRepository,
            transactionRepository: transactionRepository,
            installmentRepository: installmentRepository,
            paymentRepository: paymentRepository,
            planRepository: planRepository,
            cardRepository: cardRepository,
            investmentsService: investmentsService);

        var result = await sut.GetNetWorthSummaryAsync(userId, DateOnly.FromDateTime(DateTime.UtcNow));

        result.Assets.AccountsBalance.Should().Be(1250m);
        result.Assets.InvestmentsBalance.Should().Be(800m);
        result.Assets.TangibleAssetsBalance.Should().Be(350000m);
        result.Assets.TotalAssets.Should().Be(352050m);
        result.Liabilities.CardDebt.Should().Be(400m);
        result.Liabilities.OtherOpenLiabilities.Should().Be(150m);
        result.Liabilities.TotalLiabilities.Should().Be(550m);
        result.NetWorth.Should().Be(351500m);
        result.InvestmentPositionsCount.Should().Be(3);
        result.OpenLiabilitiesCount.Should().Be(2);
    }

    [Fact]
    public async Task GetDebtSummaryAsync_Should_Group_Cards_And_Other_Liabilities_Using_Open_Amount()
    {
        var userId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cardId = Guid.NewGuid();
        var planCard = new MoneyPlan(userId, MoneyType.Expense, "Fatura principal", 400m, ScheduleType.OneTime, today, cardId: cardId);
        var planOther = new MoneyPlan(userId, MoneyType.Expense, "Notebook", 300m, ScheduleType.OneTime, today);
        var planCardId = Guid.NewGuid();
        var planOtherId = Guid.NewGuid();
        typeof(MoneyPlan).GetProperty(nameof(MoneyPlan.Id))?.SetValue(planCard, planCardId);
        typeof(MoneyPlan).GetProperty(nameof(MoneyPlan.Id))?.SetValue(planOther, planOtherId);

        var card = new Card(userId, 1, "Henrique", "Master", "9999", "Banco", 3000m, 10, 20);
        typeof(Card).GetProperty(nameof(Card.Id))?.SetValue(card, cardId);

        var overdueCard = BuildInstallment(userId, today.AddDays(-2), 400m, InstallmentStatus.Open);
        var other = BuildInstallment(userId, today.AddDays(3), 300m, InstallmentStatus.PartiallyPaid);
        typeof(MoneyInstallment).GetProperty(nameof(MoneyInstallment.PlanId))?.SetValue(overdueCard, planCardId);
        typeof(MoneyInstallment).GetProperty(nameof(MoneyInstallment.PlanId))?.SetValue(other, planOtherId);

        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository.Setup(x => x.ListByUserAsync(userId, null, null, null, MoneyType.Expense, It.IsAny<CancellationToken>()))
            .ReturnsAsync([overdueCard, other]);

        var paymentRepository = new Mock<IMoneyPaymentRepository>();
        paymentRepository.Setup(x => x.ListByInstallmentIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MoneyPayment(other.Id, userId, DateTime.UtcNow, 120m)]);

        var planRepository = new Mock<IMoneyPlanRepository>();
        planRepository.Setup(x => x.ListByUserAsync(userId, MoneyType.Expense, It.IsAny<CancellationToken>()))
            .ReturnsAsync([planCard, planOther]);

        var cardRepository = new Mock<ICardRepository>();
        cardRepository.Setup(x => x.ListByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([card]);

        var sut = BuildSut(
            installmentRepository: installmentRepository,
            paymentRepository: paymentRepository,
            planRepository: planRepository,
            cardRepository: cardRepository);

        var result = await sut.GetDebtSummaryAsync(userId, today);

        result.TotalDebt.Should().Be(580m);
        result.CardDebt.Should().Be(400m);
        result.OtherDebt.Should().Be(180m);
        result.OverdueDebt.Should().Be(400m);
        result.DueSoonDebt.Should().Be(180m);
        result.Buckets.Should().HaveCount(3);
        result.NextItems.Should().HaveCount(2);
        result.NextItems[0].Family.Should().Be("card");
        result.NextItems[0].RelatedName.Should().Be("Master");
        result.NextItems[1].OpenAmount.Should().Be(180m);
    }

    [Fact]
    public async Task GetNetWorthHistoryAsync_Should_Build_Monthly_Series_With_Account_Investment_And_Liability_Context()
    {
        var userId = Guid.NewGuid();
        var january = new DateOnly(2026, 1, 31);
        var february = new DateOnly(2026, 2, 28);
        var march = new DateOnly(2026, 3, 31);

        var account = new Account(userId, "Conta principal", AccountType.Checking, 1000m);
        var accountId = Guid.NewGuid();
        typeof(Account).GetProperty(nameof(Account.Id))?.SetValue(account, accountId);
        typeof(Account).GetProperty(nameof(Account.CreatedAt))?.SetValue(account, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var accountRepository = new Mock<IAccountRepository>();
        accountRepository.Setup(x => x.ListByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([account]);

        var transactionRepository = new Mock<IAccountTransactionRepository>();
        transactionRepository.Setup(x => x.ListByAccountAsync(accountId, userId, null, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new AccountTransaction(accountId, userId, new DateTime(2026, 2, 10, 12, 0, 0, DateTimeKind.Utc), AccountTransactionKind.Credit, 300m, "Receita"),
                new AccountTransaction(accountId, userId, new DateTime(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc), AccountTransactionKind.Debit, 50m, "Despesa")
            ]);

        var installment = new MoneyInstallment(Guid.NewGuid(), userId, 1, new DateOnly(2026, 2, 15), 500m);
        typeof(MoneyInstallment).GetProperty(nameof(MoneyInstallment.CreatedAt))?.SetValue(installment, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository.Setup(x => x.ListByUserAsync(userId, null, null, null, MoneyType.Expense, It.IsAny<CancellationToken>()))
            .ReturnsAsync([installment]);

        var paymentRepository = new Mock<IMoneyPaymentRepository>();
        paymentRepository.Setup(x => x.ListByInstallmentIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new MoneyPayment(installment.Id, userId, new DateTime(2026, 3, 5, 12, 0, 0, DateTimeKind.Utc), 200m)
            ]);

        var investmentsService = new Mock<IInvestmentsService>();
        var position = new InvestmentPositionDto(
            Guid.NewGuid(),
            InvestmentType.ACOES,
            "PETR4",
            10m,
            20m,
            new DateOnly(2026, 1, 10),
            "Broker",
            "Ações",
            null,
            [
                new InvestmentMovementDto(Guid.NewGuid(), InvestmentMovementType.COMPRA, 5m, 20m, new DateOnly(2026, 1, 10), null),
                new InvestmentMovementDto(Guid.NewGuid(), InvestmentMovementType.COMPRA, 5m, 22m, new DateOnly(2026, 2, 10), null)
            ],
            "PETR4",
            30m);
        investmentsService.Setup(x => x.ListPositionsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([position]);
        investmentsService.Setup(x => x.EnrichWithMarketAsync(It.IsAny<List<InvestmentPositionDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<InvestmentPositionDto> items, CancellationToken _) => items);

        var sut = BuildSut(
            accountRepository: accountRepository,
            transactionRepository: transactionRepository,
            installmentRepository: installmentRepository,
            paymentRepository: paymentRepository,
            investmentsService: investmentsService);

        var result = await sut.GetNetWorthHistoryAsync(userId, 3, march);

        result.Points.Should().HaveCount(3);
        result.HasEstimatedPoints.Should().BeTrue();
        result.Points[0].ReferenceDate.Should().Be(january);
        result.Points[0].AccountsBalance.Should().Be(1000m);
        result.Points[0].InvestmentsBalance.Should().Be(100m);
        result.Points[0].TotalLiabilities.Should().Be(0m);
        result.Points[1].ReferenceDate.Should().Be(february);
        result.Points[1].AccountsBalance.Should().Be(1300m);
        result.Points[1].InvestmentsBalance.Should().Be(220m);
        result.Points[1].TotalLiabilities.Should().Be(500m);
        result.Points[2].ReferenceDate.Should().Be(march);
        result.Points[2].AccountsBalance.Should().Be(1250m);
        result.Points[2].InvestmentsBalance.Should().Be(220m);
        result.Points[2].TotalLiabilities.Should().Be(300m);
        result.Points[2].NetWorth.Should().Be(1170m);
    }

    private static AccountsService BuildSut(
        Mock<IAccountRepository>? accountRepository = null,
        Mock<IAccountTransactionRepository>? transactionRepository = null,
        Mock<IMoneyInstallmentRepository>? installmentRepository = null,
        Mock<IMoneyPaymentRepository>? paymentRepository = null,
        Mock<IMoneyPlanRepository>? planRepository = null,
        Mock<ICardRepository>? cardRepository = null,
        Mock<ILoanContractRepository>? loanContractRepository = null,
        Mock<ILoanInstallmentRepository>? loanInstallmentRepository = null,
        Mock<IInvestmentsService>? investmentsService = null,
        Mock<InvestindoEmNegocio.Application.Interfaces.ICashflowProjectionEngine>? projectionEngine = null,
        Mock<InvestindoEmNegocio.Application.Interfaces.IRiskBotService>? riskBotService = null,
        Mock<InvestindoEmNegocio.Application.Interfaces.IInsightEngineService>? insightEngineService = null,
        Mock<InvestindoEmNegocio.Application.Interfaces.IRecommendationEngineService>? recommendationEngineService = null)
    {
        return new AccountsService(
            accountRepository?.Object ?? Mock.Of<IAccountRepository>(),
            transactionRepository?.Object ?? Mock.Of<IAccountTransactionRepository>(),
            installmentRepository?.Object ?? Mock.Of<IMoneyInstallmentRepository>(),
            paymentRepository?.Object ?? Mock.Of<IMoneyPaymentRepository>(),
            planRepository?.Object ?? Mock.Of<IMoneyPlanRepository>(),
            cardRepository?.Object ?? Mock.Of<ICardRepository>(),
            loanContractRepository?.Object ?? Mock.Of<ILoanContractRepository>(),
            loanInstallmentRepository?.Object ?? Mock.Of<ILoanInstallmentRepository>(),
            investmentsService?.Object ?? Mock.Of<IInvestmentsService>(),
            projectionEngine?.Object ?? Mock.Of<InvestindoEmNegocio.Application.Interfaces.ICashflowProjectionEngine>(),
            riskBotService?.Object ?? Mock.Of<InvestindoEmNegocio.Application.Interfaces.IRiskBotService>(),
            insightEngineService?.Object ?? Mock.Of<InvestindoEmNegocio.Application.Interfaces.IInsightEngineService>(),
            recommendationEngineService?.Object ?? Mock.Of<InvestindoEmNegocio.Application.Interfaces.IRecommendationEngineService>(),
            NullLogger<AccountsService>.Instance);
    }

    private static MoneyInstallment BuildInstallment(Guid userId, DateOnly dueDate, decimal amount, InstallmentStatus status)
    {
        var installment = new MoneyInstallment(Guid.NewGuid(), userId, 1, dueDate, amount);
        typeof(MoneyInstallment).GetProperty(nameof(MoneyInstallment.Status))?.SetValue(installment, status);
        return installment;
    }
}
