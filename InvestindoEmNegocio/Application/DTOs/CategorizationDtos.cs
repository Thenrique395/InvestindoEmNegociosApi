namespace InvestindoEmNegocio.Application.DTOs;

public sealed record CategorizationSuggestionDto(
    Guid? CategoryId,
    string? CategoryName,
    decimal Confidence,
    int Score,
    string ConfidenceBand,
    string? ReasonCode
);

public sealed record RecurrenceSuggestionDto(
    bool IsRecurringCandidate,
    string Frequency,
    int Score,
    string ConfidenceBand,
    string ReasonCode,
    string? EvidenceLabel
);
