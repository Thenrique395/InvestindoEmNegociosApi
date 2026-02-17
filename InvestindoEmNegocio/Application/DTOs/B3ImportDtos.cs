using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Application.DTOs;

public sealed class UploadB3ReportRequest
{
    [FromForm(Name = "file")]
    public IFormFile? File { get; init; }
}

public sealed record ConfirmB3ImportRequest(
    string ImportToken,
    string Strategy = "merge");

public sealed record B3ExtractResponse(
    string ImportToken,
    string? ReferenceMonth,
    string? HolderName,
    string? Document,
    B3ExtractTotals Totals,
    IReadOnlyList<B3ExtractPosition> Positions,
    IReadOnlyList<B3ExtractIncome> Incomes,
    IReadOnlyList<B3ExtractTrade> Trades,
    string RawText);

public sealed record B3ExtractTotals(
    decimal Positions,
    decimal Incomes,
    int Trades);

public sealed record B3ExtractPosition(
    string Product,
    string Type,
    string Institution,
    decimal Quantity,
    decimal ClosingPrice,
    decimal UpdatedValue);

public sealed record B3ExtractIncome(
    string Product,
    string PaymentDate,
    string EventType,
    string Institution,
    decimal Quantity,
    decimal UnitPrice,
    decimal NetValue);

public sealed record B3ExtractTrade(
    string Code,
    string Period,
    string Institution,
    decimal BuyQuantity,
    decimal SellQuantity,
    decimal NetQuantity,
    decimal AvgBuyPrice,
    decimal AvgSellPrice);

public sealed record B3ConfirmImportResponse(int Imported);
