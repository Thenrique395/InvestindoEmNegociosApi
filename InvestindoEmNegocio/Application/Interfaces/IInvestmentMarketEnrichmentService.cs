using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IInvestmentMarketEnrichmentService
{
    Task<List<InvestmentPositionDto>> EnrichWithMarketAsync(List<InvestmentPositionDto> items, CancellationToken cancellationToken = default);
}
