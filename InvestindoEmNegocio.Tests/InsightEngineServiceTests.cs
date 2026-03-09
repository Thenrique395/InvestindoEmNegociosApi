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
public class InsightEngineServiceTests
{
    [Fact]
    public async Task BuildAsync_Should_Prioritize_Reactive_Insight_When_There_Are_Overdue_Expenses()
    {
        var userId = Guid.NewGuid();
        var risk = new RiskBotAssessmentResponse(
            "month",
            new DateOnly(2026, 3, 9),
            42,
            "critical",
            "critical",
            new DateOnly(2026, 3, 10),
            75m,
            90m,
            -100m,
            ["overdue_expenses", "projected_deficit"],
            ["Base: 100", "- 35 despesas atrasadas"],
            [
                new RiskBotRecommendationResponse("overdue-expenses", "danger", "Você tem despesas vencidas.", "Quitar despesas", "/despesas", new Dictionary<string, string> { ["focus"] = "overdue" }, 200m, new DateOnly(2026, 3, 8))
            ]);
        var projection = new CashflowProjectionResponse(
            "month",
            new DateOnly(2026, 3, 9),
            new DateOnly(2026, 3, 9),
            new DateOnly(2026, 3, 31),
            1000m,
            -100m,
            -150m,
            new DateOnly(2026, 3, 10),
            new DateOnly(2026, 3, 10),
            []);

        var riskBot = new Mock<IRiskBotService>();
        riskBot.Setup(x => x.AssessAsync(userId, "month", It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>())).ReturnsAsync(risk);

        var projectionEngine = new Mock<ICashflowProjectionEngine>();
        projectionEngine.Setup(x => x.ProjectAsync(userId, "month", It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>())).ReturnsAsync(projection);

        var installmentRepository = new Mock<IMoneyInstallmentRepository>();
        installmentRepository.Setup(x => x.ListByUserAsync(userId, null, projection.ProjectionStart, projection.ProjectionEnd, MoneyType.Expense, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new MoneyInstallment(Guid.NewGuid(), userId, 1, new DateOnly(2026, 3, 8), 200m),
                new MoneyInstallment(Guid.NewGuid(), userId, 2, new DateOnly(2026, 3, 11), 120m)
            ]);
        installmentRepository.Setup(x => x.ListByUserAsync(userId, InstallmentStatus.Open, projection.ProjectionStart, projection.ProjectionEnd, MoneyType.Income, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new MoneyInstallment(Guid.NewGuid(), userId, 1, new DateOnly(2026, 3, 15), 300m)
            ]);

        var profileRepository = new Mock<IUserProfileRepository>();
        profileRepository.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((UserProfile?)null);

        var sut = new InsightEngineService(riskBot.Object, projectionEngine.Object, installmentRepository.Object, profileRepository.Object);

        var result = await sut.BuildAsync(userId, "month", new DateOnly(2026, 3, 9));

        result.PrimaryInsight.Should().NotBeNull();
        result.PrimaryInsight!.Family.Should().Be("reactive");
        result.PrimaryInsight.Priority.Should().Be("critical");
        result.Insights.Should().Contain(x => x.Family == "preventive");
        result.Insights.Should().Contain(x => x.Family == "structural");
    }
}
