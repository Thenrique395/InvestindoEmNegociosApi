using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IInvestmentGoalQueryService
{
    Task<InvestmentGoalDto?> GetGoalAsync(Guid userId, CancellationToken cancellationToken = default);
}
