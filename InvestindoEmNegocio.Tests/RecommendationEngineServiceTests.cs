using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using Moq;

namespace InvestindoEmNegocio.Tests;

[Trait("Suite", "Smoke")]
public class RecommendationEngineServiceTests
{
    [Fact]
    public async Task BuildAsync_Should_Rank_Dedupe_And_Filter_By_MinScore()
    {
        var userId = Guid.NewGuid();
        var risk = new RiskBotAssessmentResponse(
            "month",
            new DateOnly(2026, 3, 9),
            62,
            "warning",
            "warning",
            null,
            80m,
            110m,
            850m,
            ["pending_income"],
            ["Base: 100"],
            [
                new RiskBotRecommendationResponse("pending-income", "info", "Próxima receita pendente.", "Abrir receitas", "/receitas", new Dictionary<string, string> { ["focus"] = "pending" }, 100m, new DateOnly(2026, 3, 10)),
                new RiskBotRecommendationResponse("due-soon-expenses", "warn", "Há despesas vencendo.", "Ver próximas despesas", "/despesas", new Dictionary<string, string> { ["focus"] = "upcoming" }, 1200m, new DateOnly(2026, 3, 11))
            ]);
        var insights = new InsightEngineResponse(
            "month",
            new DateOnly(2026, 3, 9),
            new InsightEngineItemResponse("preventive", "preventive-upcoming-window", "warning", "Janela preventiva", "Há pressão próxima.", "Acompanhar vencimentos.", 62, null, 80m, 110m, 850m, ["Cobertura"], ["Reserve caixa"], ["pending_income"], ["Base: 100"], [
                new RiskBotRecommendationResponse("pending-income", "info", "Próxima receita pendente.", "Abrir receitas", "/receitas", new Dictionary<string, string> { ["focus"] = "pending" }, 100m, new DateOnly(2026, 3, 10)),
                new RiskBotRecommendationResponse("wealth-surplus", "info", "Folga para metas.", "Abrir metas", "/metas", new Dictionary<string, string>(), 50m, null)
            ]),
            []);

        var riskBot = new Mock<IRiskBotService>();
        riskBot.Setup(x => x.AssessAsync(userId, "month", It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>())).ReturnsAsync(risk);

        var insightEngine = new Mock<IInsightEngineService>();
        insightEngine.Setup(x => x.BuildAsync(userId, "month", It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>())).ReturnsAsync(insights);

        var sut = new RecommendationEngineService(riskBot.Object, insightEngine.Object);

        var result = await sut.BuildAsync(userId, "month", new DateOnly(2026, 3, 9));

        result.MinScoreApplied.Should().Be(50);
        result.Items.Should().NotBeEmpty();
        result.Items.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        result.Items[0].Id.Should().Be("due-soon-expenses");
        result.Items.Should().NotContain(x => x.Id == "wealth-surplus");
    }
}
