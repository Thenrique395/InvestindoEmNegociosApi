using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAiFinancialHealthService
{
    Task<AiFinancialHealthResponse> GetHealthAsync(Guid userId, DateOnly? referenceDate = null, CancellationToken cancellationToken = default);
}
