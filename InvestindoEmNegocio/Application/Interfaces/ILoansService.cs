using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface ILoansService
{
    Task<IReadOnlyList<LoanContractResponse>> ListAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<LoanContractResponse> CreateAsync(Guid userId, LoanContractRequest request, CancellationToken cancellationToken = default);
    Task<LoanSimulationResponse> SimulateAsync(Guid userId, LoanContractRequest request, CancellationToken cancellationToken = default);
}
