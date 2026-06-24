using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IIncomeSummaryService
{
    Task<IncomeListResponse> GetListAsync(Guid userId, string? month, CancellationToken cancellationToken = default);
    Task<IncomeSummaryResponse> GetSummaryAsync(Guid userId, string? month, CancellationToken cancellationToken = default);
}
