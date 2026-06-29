using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class AiFinancialHealthServiceTests
{
    private static FinancialAssistantPromptContextResponse BuildContext(decimal overdueDebt = 0m, decimal totalDebt = 0m, decimal netWorth = 5000m, string cashflowPriority = "ok")
    {
        return new FinancialAssistantPromptContextResponse(
            new DateOnly(2026, 6, 29),
            new RealAvailableBalanceResponse("month", new DateOnly(2026, 6, 30), new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), 3000m, 900m, 3, 200m, 1, 2300m, 2500m, 100m, 1, 150m),
            new DebtSummaryResponse(new DateOnly(2026, 6, 30), totalDebt, 0m, totalDebt, overdueDebt, 0m, 1, [], []),
            new NetWorthSummaryResponse(new DateOnly(2026, 6, 30), new WealthAssetBreakdownResponse(3000m, 4000m, 1000m, 8000m), new WealthLiabilityBreakdownResponse(600m, 1200m, 1800m), netWorth, 2, 4, "Jun/2026"),
            new CashflowProjectionResponse("month", new DateOnly(2026, 6, 30), new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), 2300m, 2600m, 1800m, new DateOnly(2026, 6, 18), null, []),
            new RiskBotAssessmentResponse("month", new DateOnly(2026, 6, 30), 81, "ok", "normal", null, 1.3m, 1.4m, 2600m, ["healthy"], ["good coverage"], []),
            new InsightEngineResponse("month", new DateOnly(2026, 6, 30), new InsightEngineItemResponse("preventive", "cashflow", cashflowPriority, "Insight", "Mensagem", "Ação", 81, null, 1.3m, 1.4m, 2600m, [], [], [], [], []), []),
            new RecommendationEngineResponse("month", new DateOnly(2026, 6, 30), 60, []),
            "Assistente informativo.");
    }

    private static (AiFinancialHealthService Sut, Mock<IAnthropicClient> AnthropicClient, Mock<IFinancialAssistantService> Assistant) CreateSut(FinancialAssistantPromptContextResponse context)
    {
        var assistant = new Mock<IFinancialAssistantService>();
        assistant.Setup(x => x.BuildContextAsync(It.IsAny<Guid>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>())).ReturnsAsync(context);
        var anthropicClient = new Mock<IAnthropicClient>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new AiFinancialHealthService(assistant.Object, anthropicClient.Object, cache);
        return (sut, anthropicClient, assistant);
    }

    [Fact]
    public async Task GetHealthAsync_Should_Return_Ai_Verdict_When_Response_Is_Valid_Json()
    {
        var (sut, anthropicClient, _) = CreateSut(BuildContext());
        anthropicClient
            .Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""
                {"overallStatus":"warning","overallSummary":"Atenção pontual.","areas":[
                  {"area":"cashflow","status":"ok","explanation":"Caixa estável."},
                  {"area":"divida","status":"warning","explanation":"Dívida moderada."},
                  {"area":"patrimonio","status":"ok","explanation":"Patrimônio positivo."}]}
                """);

        var result = await sut.GetHealthAsync(Guid.NewGuid());

        result.GeneratedByAi.Should().BeTrue();
        result.OverallStatus.Should().Be("warning");
        result.Areas.Should().HaveCount(3);
        result.Areas.First(a => a.Area == "divida").Status.Should().Be("warning");
    }

    [Fact]
    public async Task GetHealthAsync_Should_Strip_Markdown_Fence_Before_Parsing()
    {
        var (sut, anthropicClient, _) = CreateSut(BuildContext());
        anthropicClient
            .Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""
                ```json
                {"overallStatus":"ok","overallSummary":"Tudo certo.","areas":[
                  {"area":"cashflow","status":"ok","explanation":"ok"},
                  {"area":"divida","status":"ok","explanation":"ok"},
                  {"area":"patrimonio","status":"ok","explanation":"ok"}]}
                ```
                """);

        var result = await sut.GetHealthAsync(Guid.NewGuid());

        result.GeneratedByAi.Should().BeTrue();
        result.OverallStatus.Should().Be("ok");
    }

    [Fact]
    public async Task GetHealthAsync_Should_Fallback_When_Anthropic_Unavailable()
    {
        var (sut, anthropicClient, _) = CreateSut(BuildContext(overdueDebt: 500m, totalDebt: 500m, netWorth: -100m, cashflowPriority: "critical"));
        anthropicClient
            .Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AppProblemException("Assistente indisponível", "Anthropic não configurada.", 503));

        var result = await sut.GetHealthAsync(Guid.NewGuid());

        result.GeneratedByAi.Should().BeFalse();
        result.OverallStatus.Should().Be("critical");
        result.Areas.First(a => a.Area == "cashflow").Status.Should().Be("critical");
        result.Areas.First(a => a.Area == "divida").Status.Should().Be("critical");
        result.Areas.First(a => a.Area == "patrimonio").Status.Should().Be("critical");
    }

    [Fact]
    public async Task GetHealthAsync_Should_Fallback_When_Ai_Response_Is_Invalid_Json()
    {
        var (sut, anthropicClient, _) = CreateSut(BuildContext());
        anthropicClient
            .Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("isso não é json");

        var result = await sut.GetHealthAsync(Guid.NewGuid());

        result.GeneratedByAi.Should().BeFalse();
        result.OverallStatus.Should().Be("ok");
    }

    [Fact]
    public async Task GetHealthAsync_Should_Compute_Warning_Debt_Without_Overdue()
    {
        var (sut, anthropicClient, _) = CreateSut(BuildContext(overdueDebt: 0m, totalDebt: 800m, netWorth: 100m, cashflowPriority: "ok"));
        anthropicClient
            .Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("timeout"));

        var result = await sut.GetHealthAsync(Guid.NewGuid());

        result.Areas.First(a => a.Area == "divida").Status.Should().Be("warning");
        result.OverallStatus.Should().Be("warning");
    }

    [Fact]
    public async Task GetHealthAsync_Should_Use_Cache_On_Second_Call_Same_Day()
    {
        var userId = Guid.NewGuid();
        var (sut, anthropicClient, assistant) = CreateSut(BuildContext());
        anthropicClient
            .Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"overallStatus":"ok","overallSummary":"ok","areas":[{"area":"cashflow","status":"ok","explanation":"ok"},{"area":"divida","status":"ok","explanation":"ok"},{"area":"patrimonio","status":"ok","explanation":"ok"}]}""");
        var referenceDate = new DateOnly(2026, 6, 29);

        await sut.GetHealthAsync(userId, referenceDate);
        await sut.GetHealthAsync(userId, referenceDate);

        anthropicClient.Verify(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        assistant.Verify(x => x.BuildContextAsync(userId, referenceDate, It.IsAny<CancellationToken>()), Times.Once);
    }
}
