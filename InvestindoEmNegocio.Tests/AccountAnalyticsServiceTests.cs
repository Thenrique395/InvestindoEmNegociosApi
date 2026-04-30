using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Moq;

namespace InvestindoEmNegocio.Tests;

[Trait("Suite", "Smoke")]
public class AccountAnalyticsServiceTests
{
    [Fact]
    public async Task GetRealAvailableBalanceAsync_Should_Use_Active_Accounts_And_Open_Items()
    {
        var userId = Guid.NewGuid();
        var accountA = new Account(userId, "Conta A", AccountType.Checking, 1000m);
        var accountB = new Account(userId, "Conta B", AccountType.Savings, 500m);
        var accountInactive = new Account(userId, "Conta C", AccountType.Other, 999m);
        accountInactive.Deactivate();
        var accountAId = accountA.Id;
        var accountBId = accountB.Id;
        var accountInactiveId = accountInactive.Id;

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
                BuildInstallment(userId, Guid.NewGuid(), today.AddDays(10), 50m, InstallmentStatus.Anticipated)
            ]);
        installmentRepository.Setup(x => x.ListByUserAsync(userId, InstallmentStatus.Open, It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), MoneyType.Income, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new MoneyInstallment(Guid.NewGuid(), userId, 1, today.AddDays(3), 400m)
            ]);

        var sut = BuildSut(accountRepository: accountRepository, transactionRepository: transactionRepository, installmentRepository: installmentRepository);

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
        var accountId = account.Id;

        var accountRepository = new Mock<IAccountRepository>();
        accountRepository.Setup(x => x.ListByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([account]);

        var transactionRepository = new Mock<IAccountTransactionRepository>();
        transactionRepository.Setup(x => x.SumSignedAmountByAccountAsync(accountId, userId, It.IsAny<CancellationToken>())).ReturnsAsync(250m);

        var card = new Card(userId, 1, "Henrique", "Visa Black", "1234", "Banco", 5000m, 10, 20);
        var cardId = card.Id;
        var planCard = new MoneyPlan(userId, MoneyType.Expense, "Fatura Visa", 400m, ScheduleType.OneTime, DateOnly.FromDateTime(DateTime.UtcNow), cardId: cardId);
        var planOther = new MoneyPlan(userId, MoneyType.Expense, "Curso", 200m, ScheduleType.OneTime, DateOnly.FromDateTime(DateTime.UtcNow));
        var planCardId = planCard.Id;
        var planOtherId = planOther.Id;

        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        var cardInstallment = BuildInstallment(userId, planCardId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5), 400m, InstallmentStatus.Open);
        var otherInstallment = BuildInstallment(userId, planOtherId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10), 200m, InstallmentStatus.PartiallyPaid);
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
        var card = new Card(userId, 1, "Henrique", "Master", "9999", "Banco", 3000m, 10, 20);
        var cardId = card.Id;
        var planCard = new MoneyPlan(userId, MoneyType.Expense, "Fatura principal", 400m, ScheduleType.OneTime, today, cardId: cardId);
        var planOther = new MoneyPlan(userId, MoneyType.Expense, "Notebook", 300m, ScheduleType.OneTime, today);
        var planCardId = planCard.Id;
        var planOtherId = planOther.Id;

        var overdueCard = BuildInstallment(userId, planCardId, today.AddDays(-2), 400m, InstallmentStatus.Open);
        var other = BuildInstallment(userId, planOtherId, today.AddDays(3), 300m, InstallmentStatus.PartiallyPaid);

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
        var accountId = account.Id;
        account.RestoreCreatedAt(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

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
        installment.RestoreCreatedAt(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
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

    private static AccountAnalyticsService BuildSut(
        Mock<IAccountRepository>? accountRepository = null,
        Mock<IAccountTransactionRepository>? transactionRepository = null,
        Mock<IMoneyInstallmentRepository>? installmentRepository = null,
        Mock<IMoneyPaymentRepository>? paymentRepository = null,
        Mock<IMoneyPlanRepository>? planRepository = null,
        Mock<ICardRepository>? cardRepository = null,
        Mock<ILoanContractRepository>? loanContractRepository = null,
        Mock<ILoanInstallmentRepository>? loanInstallmentRepository = null,
        Mock<IInvestmentsService>? investmentsService = null,
        Mock<ICashflowProjectionEngine>? projectionEngine = null,
        Mock<IRiskBotService>? riskBotService = null,
        Mock<IInsightEngineService>? insightEngineService = null,
        Mock<IRecommendationEngineService>? recommendationEngineService = null)
    {
        var aggregate = investmentsService?.Object ?? Mock.Of<IInvestmentsService>();
        return new AccountAnalyticsService(
            accountRepository?.Object ?? Mock.Of<IAccountRepository>(),
            transactionRepository?.Object ?? Mock.Of<IAccountTransactionRepository>(),
            installmentRepository?.Object ?? Mock.Of<IMoneyInstallmentRepository>(),
            paymentRepository?.Object ?? Mock.Of<IMoneyPaymentRepository>(),
            planRepository?.Object ?? Mock.Of<IMoneyPlanRepository>(),
            cardRepository?.Object ?? Mock.Of<ICardRepository>(),
            loanContractRepository?.Object ?? Mock.Of<ILoanContractRepository>(),
            loanInstallmentRepository?.Object ?? Mock.Of<ILoanInstallmentRepository>(),
            aggregate,
            aggregate,
            projectionEngine?.Object ?? Mock.Of<ICashflowProjectionEngine>(),
            riskBotService?.Object ?? Mock.Of<IRiskBotService>(),
            insightEngineService?.Object ?? Mock.Of<IInsightEngineService>(),
            recommendationEngineService?.Object ?? Mock.Of<IRecommendationEngineService>());
    }

    private static MoneyInstallment BuildInstallment(Guid userId, Guid planId, DateOnly dueDate, decimal amount, InstallmentStatus status)
    {
        var installment = new MoneyInstallment(planId, userId, 1, dueDate, amount);
        installment.RestoreStatus(status);
        return installment;
    }
}
