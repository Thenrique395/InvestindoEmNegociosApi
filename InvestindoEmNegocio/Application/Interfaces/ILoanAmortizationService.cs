using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface ILoanAmortizationService
{
    Task<LoanAmortizationSimulationResult> SimulateAsync(Guid userId, Guid contractId, LoanAmortizationRequest request, CancellationToken cancellationToken = default);
    Task<LoanAmortizationResult> ConfirmAsync(Guid userId, Guid contractId, LoanAmortizationRequest request, CancellationToken cancellationToken = default);
}
