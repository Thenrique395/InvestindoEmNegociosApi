using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IIncomeSummaryService
{
    Task<IncomeSummaryResponse> GetSummaryAsync(Guid userId, string? month, CancellationToken cancellationToken = default);
}
