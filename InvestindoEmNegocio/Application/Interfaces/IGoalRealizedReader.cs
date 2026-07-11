using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IGoalRealizedReader
{
    Task<(decimal Realized, decimal Pending)> ReadAsync(Goal goal, DateOnly start, DateOnly end, CancellationToken ct = default);
}
