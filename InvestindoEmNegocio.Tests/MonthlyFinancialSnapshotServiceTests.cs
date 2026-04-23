using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class MonthlyFinancialSnapshotServiceTests
{
    [Fact]
    public async Task GenerateAsync_Should_Return_Existing_Snapshot_When_Already_Persisted()
    {
        var userId = Guid.NewGuid();
        var existing = new MonthlyFinancialSnapshot(
            userId,
            2026,
            3,
            1000m,
            1200m,
            300m,
            200m,
            500m,
            10000m,
            72,
            "warning",
            "Equilibre o caixa",
            "[\"Revise despesas\"]");

        var repository = new Mock<IMonthlyFinancialSnapshotRepository>();
        repository.Setup(x => x.GetByMonthAsync(userId, 2026, 3, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var accountAnalyticsService = new Mock<IAccountAnalyticsService>(MockBehavior.Strict);
        var sut = new MonthlyFinancialSnapshotService(repository.Object, accountAnalyticsService.Object);

        var result = await sut.GenerateAsync(userId, 2026, 3);

        result.Year.Should().Be(2026);
        result.Month.Should().Be(3);
        result.RealAvailableBalance.Should().Be(1000m);
        repository.Verify(x => x.AddAsync(It.IsAny<MonthlyFinancialSnapshot>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAsync_Should_Create_Snapshot_From_Account_Summaries()
    {
        var userId = Guid.NewGuid();
        var repository = new Mock<IMonthlyFinancialSnapshotRepository>();
        repository.Setup(x => x.GetByMonthAsync(userId, 2026, 4, It.IsAny<CancellationToken>())).ReturnsAsync((MonthlyFinancialSnapshot?)null);

        var accountAnalyticsService = new Mock<IAccountAnalyticsService>();
        accountAnalyticsService
            .Setup(x => x.GetRealAvailableBalanceAsync(userId, "month", It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RealAvailableBalanceResponse("month", new DateOnly(2026, 4, 30), new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), 3000m, 900m, 3, 200m, 1, 2300m, 2500m, 100m, 1, 150m));
        accountAnalyticsService
            .Setup(x => x.GetProjectionAsync(userId, "month", It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CashflowProjectionResponse("month", new DateOnly(2026, 4, 30), new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), 2300m, 2600m, 1800m, new DateOnly(2026, 4, 18), null, []));
        accountAnalyticsService
            .Setup(x => x.GetDebtSummaryAsync(userId, It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DebtSummaryResponse(new DateOnly(2026, 4, 30), 1800m, 600m, 1200m, 0m, 300m, 4, [], []));
        accountAnalyticsService
            .Setup(x => x.GetNetWorthSummaryAsync(userId, It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NetWorthSummaryResponse(new DateOnly(2026, 4, 30), new WealthAssetBreakdownResponse(3000m, 4000m, 1000m, 8000m), new WealthLiabilityBreakdownResponse(600m, 1200m, 1800m), 6200m, 2, 4, "Abr/2026"));
        accountAnalyticsService
            .Setup(x => x.GetRiskAssessmentAsync(userId, "month", It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RiskBotAssessmentResponse("month", new DateOnly(2026, 4, 30), 81, "ok", "normal", null, 1.3m, 1.4m, 2600m, ["healthy"], ["good coverage"], []));
        accountAnalyticsService
            .Setup(x => x.GetInsightsAsync(userId, "month", It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InsightEngineResponse("month", new DateOnly(2026, 4, 30), new InsightEngineItemResponse("preventive", "cashflow", "normal", "Mantenha a reserva", "Caixa saudável", "Continuar aportes", 81, null, 1.3m, 1.4m, 2600m, [], [], [], [], []), []));
        accountAnalyticsService
            .Setup(x => x.GetRecommendationsAsync(userId, "month", It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecommendationEngineResponse("month", new DateOnly(2026, 4, 30), 60, [new RecommendationItemResponse("r1", 90, "info", "risk", "Renegociar", "Texto", "Abrir", "/debts", new Dictionary<string, string>(), [], null, null)]));

        var sut = new MonthlyFinancialSnapshotService(repository.Object, accountAnalyticsService.Object);

        var result = await sut.GenerateAsync(userId, 2026, 4);

        result.Month.Should().Be(4);
        result.RealAvailableBalance.Should().Be(2300m);
        result.ProjectedBalance.Should().Be(2600m);
        result.TotalDebt.Should().Be(1800m);
        result.NetWorth.Should().Be(6200m);
        result.RiskScore.Should().Be(81);
        result.PrimaryInsight.Should().Be("Mantenha a reserva");
        result.Recommendations.Should().ContainSingle().Which.Should().Be("Renegociar");
        repository.Verify(x => x.AddAsync(It.IsAny<MonthlyFinancialSnapshot>(), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
