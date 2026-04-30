using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IInvestmentAllocationQueryService
{
    Task<InvestmentAllocationTargetDto> GetAllocationTargetAsync(Guid userId, CancellationToken cancellationToken = default);
}
