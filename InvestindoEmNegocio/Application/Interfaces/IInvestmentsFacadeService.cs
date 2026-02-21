using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IInvestmentsFacadeService
{
    Task<InvestmentAllocationTargetDto> UpsertAllocationTargetAsync(
        Guid userId,
        UpsertInvestmentAllocationTargetRequest request,
        CancellationToken cancellationToken = default);

    Task<InvestmentPositionDto> CreatePositionAsync(
        Guid userId,
        CreateInvestmentPositionRequest request,
        CancellationToken cancellationToken = default);

    Task<InvestmentPositionDto?> UpdatePositionAsync(
        Guid userId,
        Guid positionId,
        CreateInvestmentPositionRequest request,
        CancellationToken cancellationToken = default);

    Task DeletePositionAsync(
        Guid userId,
        Guid positionId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<InvestmentMovementDto> AddMovementAsync(
        Guid userId,
        Guid positionId,
        CreateInvestmentMovementRequest request,
        CancellationToken cancellationToken = default);

    Task<MarketQuoteResponse> GetMarketQuoteAsync(string symbol, CancellationToken cancellationToken = default);
    Task<MarketProfileResponse> GetMarketProfileAsync(string symbol, CancellationToken cancellationToken = default);
    Task<MarketHistoryResponse> GetMarketHistoryAsync(string symbol, string period, CancellationToken cancellationToken = default);

    Task<B3ExtractResponse> ExtractB3Async(Guid userId, Stream pdfStream, CancellationToken cancellationToken = default);
    Task<B3ConfirmImportResponse> ConfirmB3Async(Guid userId, ConfirmB3ImportRequest request, CancellationToken cancellationToken = default);
    Task<B3SyncResponse> SyncB3Async(Guid userId, B3SyncRequest request, CancellationToken cancellationToken = default);
}
