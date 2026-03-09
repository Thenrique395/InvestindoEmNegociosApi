using InvestindoEmNegocio.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Application.DTOs;

public sealed class UploadCsvStatementRequest
{
    [FromForm(Name = "file")]
    public IFormFile? File { get; init; }

    [FromForm(Name = "accountId")]
    public Guid? AccountId { get; init; }
}

public sealed record BankStatementImportItemDto(
    string PostedAt,
    decimal Amount,
    AccountTransactionKind Kind,
    string Description,
    string? Memo,
    string? ExternalId,
    string? Type,
    Guid? CategoryId = null
);

public sealed record BankStatementPreviewItemDto(
    string PostedAt,
    decimal Amount,
    AccountTransactionKind Kind,
    string Description,
    string? Memo,
    string? ExternalId,
    string? Type,
    bool IsDuplicate,
    CategorizationSuggestionDto? SuggestedCategory = null,
    RecurrenceSuggestionDto? SuggestedRecurrence = null
);

public sealed record BankStatementImportRequest(
    Guid AccountId,
    bool SkipDuplicates,
    IReadOnlyList<BankStatementImportItemDto> Items
);

public sealed record BankStatementImportResultResponse(
    int Created,
    int Skipped
);

public sealed record CsvExtractResponse(
    string Delimiter,
    IReadOnlyList<string> DetectedColumns,
    IReadOnlyList<BankStatementPreviewItemDto> Items,
    string RawText
);
