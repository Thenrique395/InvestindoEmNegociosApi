using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IInvestmentBenchmarksService
{
    Task<InvestmentBenchmarksResponse> GetBenchmarksAsync(int months, CancellationToken cancellationToken = default);
}
