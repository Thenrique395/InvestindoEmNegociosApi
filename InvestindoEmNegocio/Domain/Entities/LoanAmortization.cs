using InvestindoEmNegocio.Domain.Common;
using InvestindoEmNegocio.Domain.Finance;

namespace InvestindoEmNegocio.Domain.Entities;

/// <summary>
/// Amortização extraordinária de um contrato: registra o valor amortizado, a estratégia e o
/// efeito estimado (saldo/prazo/parcela antes e depois + economia de juros). Preserva o histórico
/// (parcelas pagas e cronogramas anteriores nunca são apagados). A reversão marca
/// <see cref="ReversedAt"/> e estorna a movimentação em conta.
/// </summary>
public class LoanAmortization : ISoftDeletable
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ContractId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid SpaceId { get; private set; }

    public decimal Amount { get; private set; }
    public DateOnly EffectiveDate { get; private set; }
    public LoanAmortizationStrategy Strategy { get; private set; }

    public decimal PreviousBalance { get; private set; }
    public decimal NewBalance { get; private set; }
    public int PreviousTerm { get; private set; }
    public int NewTerm { get; private set; }
    public decimal PreviousPayment { get; private set; }
    public decimal NewPayment { get; private set; }
    public decimal EstimatedInterestBefore { get; private set; }
    public decimal EstimatedInterestAfter { get; private set; }
    public decimal EstimatedSavings { get; private set; }

    /// <summary>Versão do cronograma gerada por esta amortização (parcelas futuras regeneradas).</summary>
    public int ScheduleVersion { get; private set; }

    public Guid? AccountId { get; private set; }
    public int? MethodId { get; private set; }
    public Guid? AccountTransactionId { get; private set; }
    public string? ReceiptUrl { get; private set; }
    public string? Note { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? ReversedAt { get; private set; }
    public string? ReversalReason { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private LoanAmortization() { }

    public LoanAmortization(
        Guid contractId,
        Guid userId,
        Guid spaceId,
        decimal amount,
        DateOnly effectiveDate,
        LoanAmortizationStrategy strategy,
        decimal previousBalance,
        decimal newBalance,
        int previousTerm,
        int newTerm,
        decimal previousPayment,
        decimal newPayment,
        decimal estimatedInterestBefore,
        decimal estimatedInterestAfter,
        decimal estimatedSavings,
        int scheduleVersion,
        string idempotencyKey,
        Guid? accountId = null,
        int? methodId = null,
        string? note = null)
    {
        if (amount <= 0) throw new ArgumentException("Valor da amortização deve ser maior que zero.");
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("Chave de idempotência é obrigatória.");

        ContractId = contractId;
        UserId = userId;
        SpaceId = spaceId;
        Amount = amount;
        EffectiveDate = effectiveDate;
        Strategy = strategy;
        PreviousBalance = previousBalance;
        NewBalance = newBalance;
        PreviousTerm = previousTerm;
        NewTerm = newTerm;
        PreviousPayment = previousPayment;
        NewPayment = newPayment;
        EstimatedInterestBefore = estimatedInterestBefore;
        EstimatedInterestAfter = estimatedInterestAfter;
        EstimatedSavings = estimatedSavings;
        ScheduleVersion = scheduleVersion;
        IdempotencyKey = idempotencyKey;
        AccountId = accountId;
        MethodId = methodId;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
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
