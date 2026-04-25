using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;

namespace InvestindoEmNegocio.Application.Services;

public sealed class InvestmentsApplicationService(
    IInvestmentPortfolioCommandService investmentPortfolioCommandService,
    IInvestmentMarketIntegrationService investmentMarketIntegrationService) : IInvestmentsApplicationService
{
    public async Task<InvestmentAllocationTargetDto> UpsertAllocationTargetAsync(
        Guid userId,
        UpsertInvestmentAllocationTargetRequest request,
        CancellationToken cancellationToken = default)
        => await investmentPortfolioCommandService.UpsertAllocationTargetAsync(userId, request, cancellationToken);

    public async Task<InvestmentPositionDto> CreatePositionAsync(
        Guid userId,
        CreateInvestmentPositionRequest request,
        CancellationToken cancellationToken = default)
        => await investmentPortfolioCommandService.CreatePositionAsync(userId, request, cancellationToken);

    public async Task<InvestmentPositionDto?> UpdatePositionAsync(
        Guid userId,
        Guid positionId,
        CreateInvestmentPositionRequest request,
        CancellationToken cancellationToken = default)
        => await investmentPortfolioCommandService.UpdatePositionAsync(userId, positionId, request, cancellationToken);

    public async Task DeletePositionAsync(
        Guid userId,
        Guid positionId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
        => await investmentPortfolioCommandService.DeletePositionAsync(userId, positionId, ipAddress, userAgent, cancellationToken);

    public async Task<InvestmentMovementDto> AddMovementAsync(
        Guid userId,
        Guid positionId,
        CreateInvestmentMovementRequest request,
        CancellationToken cancellationToken = default)
        => await investmentPortfolioCommandService.AddMovementAsync(userId, positionId, request, cancellationToken);

    public async Task<MarketQuoteResponse> GetMarketQuoteAsync(string symbol, CancellationToken cancellationToken = default)
        => await investmentMarketIntegrationService.GetMarketQuoteAsync(symbol, cancellationToken);

    public async Task<MarketProfileResponse> GetMarketProfileAsync(string symbol, CancellationToken cancellationToken = default)
        => await investmentMarketIntegrationService.GetMarketProfileAsync(symbol, cancellationToken);

    public async Task<MarketHistoryResponse> GetMarketHistoryAsync(string symbol, string period, CancellationToken cancellationToken = default)
        => await investmentMarketIntegrationService.GetMarketHistoryAsync(symbol, period, cancellationToken);

    public async Task<B3ExtractResponse> ExtractB3Async(Guid userId, Stream pdfStream, CancellationToken cancellationToken = default)
        => await investmentMarketIntegrationService.ExtractB3Async(userId, pdfStream, cancellationToken);

    public async Task<B3ConfirmImportResponse> ConfirmB3Async(Guid userId, ConfirmB3ImportRequest request, CancellationToken cancellationToken = default)
        => await investmentMarketIntegrationService.ConfirmB3Async(userId, request, cancellationToken);

    public async Task<B3SyncResponse> SyncB3Async(Guid userId, B3SyncRequest request, CancellationToken cancellationToken = default)
        => await investmentMarketIntegrationService.SyncB3Async(userId, request, cancellationToken);
}
