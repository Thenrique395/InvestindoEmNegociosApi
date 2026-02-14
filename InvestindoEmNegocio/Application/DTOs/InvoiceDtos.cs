using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Application.DTOs;

public sealed class UploadInvoiceRequest
{
    [FromForm(Name = "file")]
    public IFormFile? File { get; init; }
}

public sealed record InvoiceItemDto(string? Date, string Description, string? Amount);

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
