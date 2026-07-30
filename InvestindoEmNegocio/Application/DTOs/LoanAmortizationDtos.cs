using InvestindoEmNegocio.Domain.Finance;

namespace InvestindoEmNegocio.Application.DTOs;

/// <summary>
/// Amortização extraordinária. <see cref="Strategy"/>: ReduceTerm (mantém parcela, reduz prazo),
/// ReducePayment (mantém prazo, reduz parcela) ou FullSettlement (quita). <see cref="IdempotencyKey"/>
/// protege contra duplicidade.
/// </summary>
public sealed record LoanAmortizationRequest(
    decimal Amount,
    LoanAmortizationStrategy Strategy,
    DateOnly? EffectiveDate = null,
    Guid? AccountId = null,
    int? MethodId = null,
    string? Note = null,
    string? IdempotencyKey = null);

/// <summary>Resultado estimado de uma amortização (antes/depois + economia). Inclui o disclaimer.</summary>
public sealed record LoanAmortizationSimulationResult(
    LoanAmortizationStrategy Strategy,
    decimal Amount,
    decimal PreviousBalance,
    decimal NewBalance,
    int PreviousTerm,
    int NewTerm,
    decimal PreviousPayment,
    decimal NewPayment,
    decimal EstimatedInterestBefore,
    decimal EstimatedInterestAfter,
    decimal EstimatedSavings,
    string Disclaimer);

/// <summary>Resultado da confirmação de uma amortização (registro + contrato + novo cronograma).</summary>
public sealed record LoanAmortizationResult(
    Guid AmortizationId,
    Guid ContractId,
    LoanAmortizationSimulationResult Simulation,
    Guid? AccountTransactionId,
    LoanContractSummary Contract,
    IReadOnlyList<LoanInstallmentResponse> Installments);
