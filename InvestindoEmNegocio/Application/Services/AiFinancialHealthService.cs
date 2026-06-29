using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace InvestindoEmNegocio.Application.Services;

/// <summary>
/// Agente único de saúde financeira: pede ao Claude um veredito estruturado por área (caixa,
/// dívida, patrimônio) a partir do mesmo contexto agregado já usado pelo Assistente Financeiro.
/// Se a IA falhar por qualquer motivo, cai num veredito determinístico construído a partir de
/// números já calculados pelo motor de regras existente (nunca quebra por dependência externa).
/// </summary>
public sealed class AiFinancialHealthService(
    IFinancialAssistantService financialAssistantService,
    IAnthropicClient anthropicClient,
    IMemoryCache cache) : IAiFinancialHealthService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(20);
    private static readonly string[] ValidStatuses = ["critical", "warning", "ok"];
    private static readonly string[] ExpectedAreas = ["cashflow", "divida", "patrimonio"];

    private const string SystemPrompt =
        "Você é um agente de análise de saúde financeira. Receberá um contexto JSON com dados " +
        "financeiros já consolidados de um usuário (saldo, dívidas, patrimônio, projeção de " +
        "caixa, risco). Responda ESTRITAMENTE com um JSON (sem texto antes ou depois, sem bloco " +
        "de código markdown) no formato exato: " +
        "{\"overallStatus\":\"critical|warning|ok\",\"overallSummary\":\"frase curta em português\"," +
        "\"areas\":[{\"area\":\"cashflow\",\"status\":\"critical|warning|ok\",\"explanation\":\"...\"}," +
        "{\"area\":\"divida\",\"status\":\"critical|warning|ok\",\"explanation\":\"...\"}," +
        "{\"area\":\"patrimonio\",\"status\":\"critical|warning|ok\",\"explanation\":\"...\"}]}. " +
        "Use somente os valores \"critical\", \"warning\" ou \"ok\" para status (nunca outro " +
        "valor), sempre as 3 áreas \"cashflow\", \"divida\" e \"patrimonio\", e nunca invente " +
        "números que não estejam no contexto fornecido.";

    public async Task<AiFinancialHealthResponse> GetHealthAsync(Guid userId, DateOnly? referenceDate = null, CancellationToken cancellationToken = default)
    {
        var anchor = referenceDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var cacheKey = CacheKey(userId, anchor);
        if (cache.TryGetValue(cacheKey, out AiFinancialHealthResponse? cached) && cached is not null)
            return cached;

        var context = await financialAssistantService.BuildContextAsync(userId, anchor, cancellationToken);

        AiFinancialHealthResponse result;
        try
        {
            var userMessage = $"Contexto financeiro (JSON): {JsonSerializer.Serialize(context)}";
            var raw = await anthropicClient.CompleteAsync(SystemPrompt, userMessage, cancellationToken);
            result = ParseAiResponse(anchor, raw);
        }
        catch (Exception ex) when (ex is AppProblemException or HttpRequestException or TaskCanceledException or JsonException)
        {
            result = BuildFallback(anchor, context);
        }

        cache.Set(cacheKey, result, CacheTtl);
        return result;
    }

    private static AiFinancialHealthResponse ParseAiResponse(DateOnly anchor, string raw)
    {
        var json = StripMarkdownFence(raw);
        var parsed = JsonSerializer.Deserialize<AiHealthJsonShape>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new JsonException("Resposta vazia da IA.");

        if (string.IsNullOrWhiteSpace(parsed.OverallStatus) || !ValidStatuses.Contains(parsed.OverallStatus))
            throw new JsonException("overallStatus inválido.");

        if (parsed.Areas is null || parsed.Areas.Count == 0)
            throw new JsonException("areas ausente.");

        var areas = parsed.Areas
            .Where(a => !string.IsNullOrWhiteSpace(a.Area) && !string.IsNullOrWhiteSpace(a.Status) && ValidStatuses.Contains(a.Status))
            .Select(a => new AiHealthAreaVerdict(a.Area!, a.Status!, a.Explanation ?? string.Empty))
            .ToList();

        if (!ExpectedAreas.All(expected => areas.Any(a => a.Area == expected)))
            throw new JsonException("nem todas as áreas esperadas vieram na resposta.");

        return new AiFinancialHealthResponse(anchor, parsed.OverallStatus, parsed.OverallSummary ?? string.Empty, areas, GeneratedByAi: true);
    }

    private static string StripMarkdownFence(string raw)
    {
        var trimmed = raw.Trim();
        var match = Regex.Match(trimmed, @"^```(?:json)?\s*(.*?)\s*```$", RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value.Trim() : trimmed;
    }

    private static AiFinancialHealthResponse BuildFallback(DateOnly anchor, FinancialAssistantPromptContextResponse context)
    {
        var cashflow = context.Insights.PrimaryInsight?.Priority ?? "ok";
        var divida = context.Debts.OverdueDebt > 0m ? "critical" : context.Debts.TotalDebt > 0m ? "warning" : "ok";
        var patrimonio = context.NetWorth.NetWorth < 0m ? "critical" : "ok";

        var areas = new List<AiHealthAreaVerdict>
        {
            new("cashflow", cashflow, "Avaliação baseada no motor de regras local (IA indisponível)."),
            new("divida", divida, "Avaliação baseada no motor de regras local (IA indisponível)."),
            new("patrimonio", patrimonio, "Avaliação baseada no motor de regras local (IA indisponível).")
        };

        var overall = Worst(cashflow, divida, patrimonio);
        var summary = overall switch
        {
            "critical" => "Atenção: ao menos uma área financeira está em situação crítica.",
            "warning" => "Algumas áreas financeiras pedem atenção.",
            _ => "Situação financeira estável nas áreas avaliadas."
        };

        return new AiFinancialHealthResponse(anchor, overall, summary, areas, GeneratedByAi: false);
    }

    private static string Worst(params string[] statuses)
    {
        if (statuses.Contains("critical")) return "critical";
        if (statuses.Contains("warning")) return "warning";
        return "ok";
    }

    private static string CacheKey(Guid userId, DateOnly referenceDate) => $"ai-health:{userId}:{referenceDate:yyyy-MM-dd}";

    private sealed class AiHealthJsonShape
    {
        [JsonPropertyName("overallStatus")] public string? OverallStatus { get; set; }
        [JsonPropertyName("overallSummary")] public string? OverallSummary { get; set; }
        [JsonPropertyName("areas")] public List<AiHealthAreaJsonShape>? Areas { get; set; }
    }

    private sealed class AiHealthAreaJsonShape
    {
        [JsonPropertyName("area")] public string? Area { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("explanation")] public string? Explanation { get; set; }
    }
}
