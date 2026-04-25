using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IInvestmentMarketIntegrationService
{
    Task<MarketQuoteResponse> GetMarketQuoteAsync(string symbol, CancellationToken cancellationToken = default);
    Task<MarketProfileResponse> GetMarketProfileAsync(string symbol, CancellationToken cancellationToken = default);
    Task<MarketHistoryResponse> GetMarketHistoryAsync(string symbol, string period, CancellationToken cancellationToken = default);
    Task<B3ExtractResponse> ExtractB3Async(Guid userId, Stream pdfStream, CancellationToken cancellationToken = default);
    Task<B3ConfirmImportResponse> ConfirmB3Async(Guid userId, ConfirmB3ImportRequest request, CancellationToken cancellationToken = default);
    Task<B3SyncResponse> SyncB3Async(Guid userId, B3SyncRequest request, CancellationToken cancellationToken = default);
}
