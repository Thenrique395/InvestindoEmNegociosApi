using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface ILoanTimelineService
{
    Task<IReadOnlyList<LoanTimelineEvent>> GetAsync(Guid userId, Guid contractId, CancellationToken cancellationToken = default);
}
