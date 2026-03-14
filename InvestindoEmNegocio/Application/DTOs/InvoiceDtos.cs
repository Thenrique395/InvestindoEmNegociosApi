using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Application.DTOs;

public sealed class UploadInvoiceRequest
{
    [FromForm(Name = "file")]
    public IFormFile? File { get; init; }
}

public sealed record InvoiceItemDto(
    string? Date,
    string Description,
    string? Amount,
    bool IsInstallment = false,
    int? InstallmentCurrent = null,
    int? InstallmentTotal = null,
    string? BaseDescription = null,
    Guid? SuggestedCategoryId = null,
    string? SuggestedCategoryName = null,
    decimal? SuggestedCategoryConfidence = null,
    int? SuggestedCategoryScore = null,
    string? SuggestedCategoryConfidenceBand = null,
    string? SuggestedCategoryReasonCode = null,
    RecurrenceSuggestionDto? SuggestedRecurrence = null
);

public sealed record InvoiceExtractResponse(
    string? Total,
    string? DueDate,
    string? CloseDate,
    string? CardName,
    string? BankName,
    IReadOnlyList<InvoiceItemDto> Items,
    string RawText,
    string? CardHolder,
    string? CardLast4,
    string? MinimumPayment,
    string? LimitTotal,
    string? LimitUsed,
    string? LimitAvailable,
    string? PreviousBalance,
    string? TotalDebitsBrazil,
    string? TotalPayments,
    string? TotalCredits,
    string? CurrentBalance
);

public sealed record InvoiceImportItemRequest(
    string? Date,
    string Description,
    string? Amount,
    bool IsInstallment = false,
    int? InstallmentCurrent = null,
    int? InstallmentTotal = null,
    string? BaseDescription = null,
    Guid? CategoryId = null
);

public sealed record InvoiceImportRequest(
    Guid? CardId,
    Guid? CategoryId,
    string? DefaultDueDate,
    string? StatementCloseDate,
    string? InvoiceTotal,
    string? ImportIdempotencyKey,
    bool SkipDuplicates,
    IReadOnlyList<InvoiceImportItemRequest> Items
);

public sealed record InvoiceImportResultResponse(
    int Created,
    int Skipped,
    int Failed
);

public sealed record InvoiceReconciliationItemResponse(
    string Description,
    string? BaseDescription,
    string? Date,
    decimal Amount,
    bool IsDuplicate,
    string MatchReason,
    int StatementYear,
    int StatementMonth,
    string StatementReference,
    DateOnly StatementDueDate,
    Guid? ExistingInstallmentId
);

public sealed record InvoiceReconciliationCycleResponse(
    int StatementYear,
    int StatementMonth,
    DateOnly StatementCloseDate,
    DateOnly StatementDueDate,
    string StatementReference,
    decimal CurrentTotalAmount,
    decimal ImportedNewAmount,
    decimal DuplicateAmount,
    decimal ProjectedTotalAmount,
    decimal? ParsedInvoiceTotalAmount,
    decimal? DifferenceAmount,
    int ExistingItemsCount,
    int ImportedNewItemsCount,
    int DuplicateItemsCount,
    bool ReadyToClose
);

public sealed record InvoiceReconciliationResponse(
    Guid CardId,
    string CardName,
    string? ParsedInvoiceTotal,
    string? ParsedDueDate,
    string? ParsedCloseDate,
    int TotalItems,
    int NewItems,
    int DuplicateItems,
    IReadOnlyList<InvoiceReconciliationItemResponse> Items,
    IReadOnlyList<InvoiceReconciliationCycleResponse> Cycles
);
