using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Application.DTOs;

public sealed class UploadOfxRequest
{
    [FromForm(Name = "file")]
    public IFormFile? File { get; init; }

    [FromForm(Name = "accountId")]
    public Guid? AccountId { get; init; }
}

public sealed record OfxExtractResponse(
    string? BankId,
    string? BranchId,
    string? AccountNumber,
    string? AccountType,
    string? StartDate,
    string? EndDate,
    decimal? LedgerBalance,
    IReadOnlyList<BankStatementPreviewItemDto> Items,
    string RawText
);
