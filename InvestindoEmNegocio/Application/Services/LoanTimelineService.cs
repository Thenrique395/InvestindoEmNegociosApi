using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Finance;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;

namespace InvestindoEmNegocio.Application.Services;

/// <summary>
/// Monta a linha do tempo de um contrato agregando os eventos do próprio contrato, dos pagamentos
/// e das amortizações — sem persistir nada (fonte oficial são as entidades existentes).
/// </summary>
public class LoanTimelineService(
    ILoanContractRepository contractRepository,
    ILoanPaymentRepository paymentRepository,
    ILoanAmortizationRepository amortizationRepository) : ILoanTimelineService
{
    public async Task<IReadOnlyList<LoanTimelineEvent>> GetAsync(Guid userId, Guid contractId, CancellationToken cancellationToken = default)
    {
        var contract = await contractRepository.GetByIdAsync(contractId, userId, cancellationToken)
            ?? throw new AppProblemException("Contrato não encontrado", "O contrato informado não existe ou não pertence ao usuário.", StatusCodes.Status404NotFound);

        var payments = await paymentRepository.ListByContractAsync(contractId, userId, cancellationToken);
        var amortizations = await amortizationRepository.ListByContractAsync(contractId, userId, cancellationToken);

        var events = new List<LoanTimelineEvent>
        {
            new(contract.CreatedAt, "contract_created", "Contrato criado", contract.PrincipalAmount)
        };

        foreach (var payment in payments)
        {
            events.Add(new LoanTimelineEvent(payment.PaidAt, "installment_paid", "Parcela paga", payment.Amount));
            if (payment.ReversedAt is DateTime reversedAt)
                events.Add(new LoanTimelineEvent(reversedAt, "payment_reversed", "Pagamento estornado", payment.Amount));
        }

        foreach (var amortization in amortizations)
        {
            events.Add(new LoanTimelineEvent(amortization.CreatedAt, "amortization", $"Amortização — {StrategyLabel(amortization.Strategy)}", amortization.Amount));
            if (amortization.ReversedAt is DateTime reversedAt)
                events.Add(new LoanTimelineEvent(reversedAt, "amortization_reversed", "Amortização estornada", amortization.Amount));
        }

        if (contract.ClosedAt is DateTime closedAt)
            events.Add(new LoanTimelineEvent(closedAt, "contract_closed", "Contrato quitado", null));
        if (contract.ArchivedAt is DateTime archivedAt)
            events.Add(new LoanTimelineEvent(archivedAt, "contract_archived", "Contrato arquivado", null));

        return events.OrderByDescending(e => e.At).ToList();
    }

    private static string StrategyLabel(LoanAmortizationStrategy strategy) => strategy switch
    {
        LoanAmortizationStrategy.ReduceTerm => "reduzir prazo",
        LoanAmortizationStrategy.ReducePayment => "reduzir parcela",
        LoanAmortizationStrategy.FullSettlement => "quitação",
        _ => strategy.ToString()
    };
}
