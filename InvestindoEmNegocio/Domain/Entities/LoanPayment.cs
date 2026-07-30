using InvestindoEmNegocio.Domain.Common;

namespace InvestindoEmNegocio.Domain.Entities;

/// <summary>
/// Pagamento de uma parcela de empréstimo/financiamento. Histórico próprio (nunca é apagado
/// quando a parcela é atualizada). A reversão não apaga o pagamento: marca <see cref="ReversedAt"/>
/// e gera uma movimentação de estorno em conta.
/// </summary>
public class LoanPayment : ISoftDeletable
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ContractId { get; private set; }
    public Guid InstallmentId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid SpaceId { get; private set; }

    public DateTime PaidAt { get; private set; }
    public decimal Amount { get; private set; }
    public decimal PrincipalAmount { get; private set; }
    public decimal InterestAmount { get; private set; }
    public decimal PenaltyAmount { get; private set; }
    public decimal DiscountAmount { get; private set; }

    public Guid? AccountId { get; private set; }
    public int? MethodId { get; private set; }
    public Guid? AccountTransactionId { get; private set; }
    public string? ReceiptUrl { get; private set; }
    public string? Note { get; private set; }

    /// <summary>Chave de idempotência (única por usuário) — impede pagamento/despesa/movimentação duplicados.</summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? ReversedAt { get; private set; }
    public string? ReversalReason { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private LoanPayment() { }

    public LoanPayment(
        Guid contractId,
        Guid installmentId,
        Guid userId,
        Guid spaceId,
        DateTime paidAt,
        decimal amount,
        decimal principalAmount,
        decimal interestAmount,
        string idempotencyKey,
        decimal penaltyAmount = 0m,
        decimal discountAmount = 0m,
        Guid? accountId = null,
        int? methodId = null,
        string? note = null)
    {
        if (amount <= 0) throw new ArgumentException("Valor do pagamento deve ser maior que zero.");
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("Chave de idempotência é obrigatória.");

        ContractId = contractId;
        InstallmentId = installmentId;
        UserId = userId;
        SpaceId = spaceId;
        PaidAt = paidAt.Kind == DateTimeKind.Utc ? paidAt : DateTime.SpecifyKind(paidAt, DateTimeKind.Utc);
        Amount = amount;
        PrincipalAmount = principalAmount;
        InterestAmount = interestAmount;
        PenaltyAmount = penaltyAmount;
        DiscountAmount = discountAmount;
        AccountId = accountId;
        MethodId = methodId;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        IdempotencyKey = idempotencyKey;
    }

    public bool IsReversed => ReversedAt is not null;

    public void LinkAccountTransaction(Guid transactionId) => AccountTransactionId = transactionId;

    public void AttachReceipt(string url) => ReceiptUrl = url;

    public void MarkReversed(DateTime whenUtc, string? reason)
    {
        ReversedAt = whenUtc.Kind == DateTimeKind.Utc ? whenUtc : DateTime.SpecifyKind(whenUtc, DateTimeKind.Utc);
        ReversalReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    public void MarkDeleted(DateTime nowUtc) => DeletedAt = nowUtc;
}
