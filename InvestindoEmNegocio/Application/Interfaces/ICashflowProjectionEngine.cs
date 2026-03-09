using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface ICashflowProjectionEngine
{
    Task<CashflowProjectionResponse> ProjectAsync(
        Guid userId,
        string period = "month",
        DateOnly? referenceDate = null,
        CancellationToken cancellationToken = default);
}
