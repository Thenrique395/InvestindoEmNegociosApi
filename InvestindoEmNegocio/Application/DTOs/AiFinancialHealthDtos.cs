namespace InvestindoEmNegocio.Application.DTOs;

public sealed record AiHealthAreaVerdict(string Area, string Status, string Explanation);

public sealed record AiFinancialHealthResponse(
    DateOnly ReferenceDate,
    string OverallStatus,
    string OverallSummary,
    IReadOnlyList<AiHealthAreaVerdict> Areas,
    bool GeneratedByAi);
