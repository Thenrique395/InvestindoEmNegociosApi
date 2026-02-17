namespace InvestindoEmNegocio.Application.DTOs;

public sealed class B3ApiOptions
{
    public bool Enabled { get; set; }
    public string? BaseUrl { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
}

public sealed record B3ConsentStatusResponse(
    bool HasConsent,
    string Provider,
    DateTime? UpdatedAtUtc,
    string Message);

public sealed record B3SyncRequest(
    string Strategy = "merge",
    string? FallbackImportToken = null);

public sealed record B3SyncResponse(
    string Source,
    bool FallbackUsed,
    int Imported,
    string Message);

public sealed record B3ImportSnapshot(
    string? ReferenceMonth,
    string? HolderName,
    string? Document,
    IReadOnlyList<B3ExtractPosition> Positions,
    IReadOnlyList<B3ExtractIncome> Incomes,
    IReadOnlyList<B3ExtractTrade> Trades);
