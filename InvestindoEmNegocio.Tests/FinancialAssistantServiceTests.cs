using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class FinancialAssistantServiceTests
{
    private static (FinancialAssistantService Sut, Mock<IAnthropicClient> AnthropicClient) CreateSut(Guid userId)
    {
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

        var anthropicClient = new Mock<IAnthropicClient>();
        var sut = new FinancialAssistantService(accountAnalyticsService.Object, anthropicClient.Object);
        return (sut, anthropicClient);
    }

    [Fact]
    public async Task ChatAsync_Should_Reject_Empty_Question_Without_Calling_Anthropic()
    {
        var userId = Guid.NewGuid();
        var (sut, anthropicClient) = CreateSut(userId);

        var result = await sut.ChatAsync(userId, new FinancialAssistantChatRequest("   "));

        result.Allowed.Should().BeFalse();
        result.ReasonCode.Should().Be("empty_question");
        anthropicClient.Verify(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChatAsync_Should_Reject_Question_Too_Long_Without_Calling_Anthropic()
    {
        var userId = Guid.NewGuid();
        var (sut, anthropicClient) = CreateSut(userId);
        var longQuestion = new string('a', 601);

        var result = await sut.ChatAsync(userId, new FinancialAssistantChatRequest(longQuestion));

        result.Allowed.Should().BeFalse();
        result.ReasonCode.Should().Be("question_too_long");
        anthropicClient.Verify(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChatAsync_Should_Block_Automation_Topics_Without_Calling_Anthropic()
    {
        var userId = Guid.NewGuid();
        var (sut, anthropicClient) = CreateSut(userId);

        var result = await sut.ChatAsync(userId, new FinancialAssistantChatRequest("Pode transfira dinheiro da minha conta?"));

        result.Allowed.Should().BeFalse();
        result.ReasonCode.Should().Be("blocked_automation");
        anthropicClient.Verify(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChatAsync_Should_Return_Anthropic_Answer_When_Call_Succeeds()
    {
        var userId = Guid.NewGuid();
        var (sut, anthropicClient) = CreateSut(userId);
        anthropicClient
            .Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Resposta gerada pela IA com base no seu contexto financeiro.");

        var result = await sut.ChatAsync(userId, new FinancialAssistantChatRequest("Como está meu risco?"));

        result.Allowed.Should().BeTrue();
        result.ReasonCode.Should().Be("ok");
        result.Answer.Should().Be("Resposta gerada pela IA com base no seu contexto financeiro.");
    }

    [Fact]
    public async Task ChatAsync_Should_Fallback_To_Rule_Engine_When_Anthropic_Is_Unavailable()
    {
        var userId = Guid.NewGuid();
        var (sut, anthropicClient) = CreateSut(userId);
        anthropicClient
            .Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AppProblemException("Assistente indisponível", "Anthropic não configurada.", 503));

        var result = await sut.ChatAsync(userId, new FinancialAssistantChatRequest("Como está meu risco?"));

        result.Allowed.Should().BeTrue();
        result.ReasonCode.Should().Be("ok_fallback");
        result.Answer.Should().Contain("score de risco atual é 81/100");
    }

    [Fact]
    public async Task ChatAsync_Should_Fallback_To_Rule_Engine_When_Anthropic_Throws_Http_Error()
    {
        var userId = Guid.NewGuid();
        var (sut, anthropicClient) = CreateSut(userId);
        anthropicClient
            .Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("timeout"));

        var result = await sut.ChatAsync(userId, new FinancialAssistantChatRequest("Qual meu patrimônio?"));

        result.Allowed.Should().BeTrue();
        result.ReasonCode.Should().Be("ok_fallback");
        result.Answer.Should().Contain("patrimônio líquido consolidado");
    }
}
