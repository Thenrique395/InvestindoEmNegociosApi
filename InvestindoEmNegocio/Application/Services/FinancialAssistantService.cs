using System.Text.Json;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;

namespace InvestindoEmNegocio.Application.Services;

public class FinancialAssistantService(
    IAccountAnalyticsService accountAnalyticsService,
    IAnthropicClient anthropicClient) : IFinancialAssistantService
{
    private const int MaxQuestionLength = 600;
    private static readonly string[] BlockedTopics =
    [
        "executa pagamento",
        "transfira dinheiro",
        "movimente meus recursos",
        "faça investimento automaticamente"
    ];

    // Pedidos de recomendação de investimento — fora do escopo do produto (só análise/organização).
    private static readonly string[] RecommendationTopics =
    [
        "que ação comprar",
        "qual ação comprar",
        "que ativo comprar",
        "qual ativo comprar",
        "onde investir",
        "onde aplicar",
        "melhor investimento",
        "melhor ação",
        "melhor ativo",
        "melhor fundo",
        "devo comprar",
        "devo vender",
        "vale a pena investir",
        "vale a pena comprar",
        "vai render",
        "qual criptomoeda",
        "que criptomoeda",
        "qual cripto",
        "que cripto"
    ];

    private const string SystemPrompt =
        "Você é o assistente financeiro do app Investindo em Negócios. Responda só com base " +
        "no contexto JSON fornecido pelo usuário (dados financeiros já consolidados dele) — " +
        "não invente números que não estejam lá. Responda em português, de forma objetiva e " +
        "curta (poucas frases). Nunca executa ações nem instrui o usuário a automatizar " +
        "movimentações financeiras. Nunca recomende compra ou venda de ativos, produtos ou " +
        "investimentos específicos, e nunca prometa nem estime rendimentos futuros. Você apenas " +
        "analisa os dados e ajuda a organizar e planejar; se pedirem recomendação de investimento, " +
        "explique que não faz recomendações e redirecione para a análise dos dados do usuário. " +
        "Não substitui aconselhamento financeiro, jurídico, tributário ou regulado.";

    public async Task<FinancialAssistantPromptContextResponse> BuildContextAsync(Guid userId, DateOnly? referenceDate = null, CancellationToken cancellationToken = default)
    {
        var anchor = referenceDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var realBalance = await accountAnalyticsService.GetRealAvailableBalanceAsync(userId, "month", anchor, cancellationToken);
        var debts = await accountAnalyticsService.GetDebtSummaryAsync(userId, anchor, cancellationToken);
        var netWorth = await accountAnalyticsService.GetNetWorthSummaryAsync(userId, anchor, cancellationToken);
        var projection = await accountAnalyticsService.GetProjectionAsync(userId, "month", anchor, cancellationToken);
        var risk = await accountAnalyticsService.GetRiskAssessmentAsync(userId, "month", anchor, cancellationToken);
        var insights = await accountAnalyticsService.GetInsightsAsync(userId, "month", anchor, cancellationToken);
        var recommendations = await accountAnalyticsService.GetRecommendationsAsync(userId, "month", anchor, cancellationToken);

        return new FinancialAssistantPromptContextResponse(
            anchor,
            realBalance,
            debts,
            netWorth,
            projection,
            risk,
            insights,
            recommendations,
            "Assistente informativo: não executa transações, não substitui aconselhamento regulado e responde com base nos dados financeiros consolidados do usuário.");
    }

    public async Task<FinancialAssistantChatResponse> ChatAsync(Guid userId, FinancialAssistantChatRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedQuestion = (request.Question ?? string.Empty).Trim();
        var context = await BuildContextAsync(userId, request.ReferenceDate, cancellationToken);

        if (string.IsNullOrWhiteSpace(normalizedQuestion))
        {
            return new FinancialAssistantChatResponse(false, normalizedQuestion, "Envie uma pergunta objetiva para o assistente.", BuildDisclaimer(), "empty_question", context);
        }

        if (normalizedQuestion.Length > MaxQuestionLength)
        {
            return new FinancialAssistantChatResponse(false, normalizedQuestion, $"A pergunta excede o limite de {MaxQuestionLength} caracteres.", BuildDisclaimer(), "question_too_long", context);
        }

        var lowered = normalizedQuestion.ToLowerInvariant();
        var blockedTopic = BlockedTopics.FirstOrDefault(lowered.Contains);
        if (blockedTopic is not null)
        {
            return new FinancialAssistantChatResponse(false, normalizedQuestion, "Posso orientar e priorizar ações, mas não executo movimentações financeiras em seu nome.", BuildDisclaimer(), "blocked_automation", context);
        }

        var recommendationTopic = RecommendationTopics.FirstOrDefault(lowered.Contains);
        if (recommendationTopic is not null)
        {
            return new FinancialAssistantChatResponse(false, normalizedQuestion, "Não recomendo compra ou venda de ativos, produtos ou investimentos específicos, nem faço promessas de rendimento. Posso analisar seus dados e ajudar a organizar e planejar suas finanças.", BuildDisclaimer(), "blocked_recommendation", context);
        }

        try
        {
            var userMessage = $"Pergunta do usuário: {normalizedQuestion}\n\nContexto financeiro (JSON): {JsonSerializer.Serialize(context)}";
            var aiAnswer = await anthropicClient.CompleteAsync(SystemPrompt, userMessage, cancellationToken);
            return new FinancialAssistantChatResponse(true, normalizedQuestion, aiAnswer, BuildDisclaimer(), "ok", context);
        }
        catch (Exception ex) when (ex is AppProblemException or HttpRequestException or TaskCanceledException)
        {
            var fallbackAnswer = BuildAnswer(lowered, context);
            return new FinancialAssistantChatResponse(true, normalizedQuestion, fallbackAnswer, BuildDisclaimer(), "ok_fallback", context);
        }
    }

    private static string BuildAnswer(string question, FinancialAssistantPromptContextResponse context)
    {
        if (question.Contains("risco"))
        {
            return $"Seu score de risco atual é {context.Risk.Score}/100 ({context.Risk.Classification}). A data provável de risco é {context.Projection.RiskDate?.ToString("dd/MM/yyyy") ?? "não identificada"} e o menor saldo projetado do período é {context.Projection.LowestBalance:C2}.";
        }

        if (question.Contains("dívida") || question.Contains("divida") || question.Contains("emprést"))
        {
            var nextDebt = context.Debts.NextItems.FirstOrDefault();
            return $"Sua dívida aberta total está em {context.Debts.TotalDebt:C2}. O próximo compromisso relevante é {nextDebt?.Title ?? "não identificado"} com vencimento em {nextDebt?.DueDate.ToString("dd/MM/yyyy") ?? "n/d"}.";
        }

        if (question.Contains("patrim") || question.Contains("riqueza"))
        {
            return $"Seu patrimônio líquido consolidado está em {context.NetWorth.NetWorth:C2}, com ativos totais de {context.NetWorth.Assets.TotalAssets:C2} e passivos de {context.NetWorth.Liabilities.TotalLiabilities:C2}.";
        }

        if (question.Contains("fazer") || question.Contains("próximo passo") || question.Contains("proximo passo"))
        {
            var top = context.Recommendations.Items.FirstOrDefault();
            return top is null
                ? $"Seu principal insight hoje é: {context.Insights.PrimaryInsight?.Title ?? "sem insight prioritário"}."
                : $"A ação mais prioritária agora é: {top.Title}. Motivo: {top.Text}";
        }

        return $"Resumo atual: saldo disponível real de {context.RealBalance.RealAvailableBalance:C2}, projeção de fechamento em {context.Projection.ProjectedClosingBalance:C2}, dívida total de {context.Debts.TotalDebt:C2} e patrimônio líquido de {context.NetWorth.NetWorth:C2}. Insight principal: {context.Insights.PrimaryInsight?.Title ?? "sem insight prioritário"}.";
    }

    private static string BuildDisclaimer()
        => "Resposta orientativa baseada nos dados consolidados do sistema. Não substitui aconselhamento financeiro, jurídico, tributário ou regulado.";
}
