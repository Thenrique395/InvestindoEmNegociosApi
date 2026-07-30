using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.DTOs;

/// <summary>
/// Pagamento de uma parcela de empréstimo. <see cref="AmountPaid"/> é opcional — quando ausente,
/// usa o total da parcela + multa − desconto. <see cref="IdempotencyKey"/> protege contra
/// duplicidade (repetir a requisição não gera novo pagamento/despesa/movimentação).
/// </summary>
public sealed record LoanPaymentRequest(
    DateTime PaidAt,
    decimal? AmountPaid = null,
    decimal PenaltyAmount = 0m,
    decimal DiscountAmount = 0m,
    Guid? AccountId = null,
    int? MethodId = null,
    string? Note = null,
    string? IdempotencyKey = null);

/// <summary>Reversão (estorno) de um pagamento de parcela. O motivo é opcional.</summary>
public sealed record LoanPaymentReversalRequest(string? Reason = null);

/// <summary>Item do histórico de pagamentos de uma parcela.</summary>
public sealed record LoanPaymentHistoryItem(
    Guid Id,
    DateTime PaidAt,
    decimal Amount,
    decimal PrincipalAmount,
    decimal InterestAmount,
    decimal PenaltyAmount,
    decimal DiscountAmount,
    Guid? AccountId,
    string? Note,
    string? ReceiptUrl,
    bool IsReversed,
    DateTime? ReversedAt);

/// <summary>Resumo do contrato devolvido após uma operação de pagamento (fonte oficial do estado).</summary>
public sealed record LoanContractSummary(
    Guid Id,
    LoanStatus Status,
    decimal OpenBalance,
    decimal PaidAmount,
    decimal PaidPrincipal,
    decimal PaidInterest,
    int OpenInstallments,
    DateOnly? NextDueDate,
    decimal MonthlyPayment);

/// <summary>Resultado de um pagamento: o pagamento, a parcela atualizada e o resumo do contrato.</summary>
public sealed record LoanPaymentResult(
    Guid PaymentId,
    Guid ContractId,
    Guid InstallmentId,
    decimal Amount,
    decimal PrincipalAmount,
    decimal InterestAmount,
    decimal PenaltyAmount,
    decimal DiscountAmount,
    DateTime PaidAt,
    Guid? AccountTransactionId,
    string? ReceiptUrl,
    LoanInstallmentResponse Installment,
    LoanContractSummary Contract);
