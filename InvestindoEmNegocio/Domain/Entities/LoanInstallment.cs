using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Entities;

public class LoanInstallment
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ContractId { get; private set; }
    public Guid UserId { get; private set; }
    public int InstallmentNo { get; private set; }
    public DateOnly DueDate { get; private set; }
    public decimal BeginningBalance { get; private set; }
    public decimal PrincipalAmount { get; private set; }
    public decimal InterestAmount { get; private set; }

    // Encargos e descontos (default 0; preenchidos na simulação/pagamento).
    public decimal InsuranceAmount { get; private set; }
    public decimal FeeAmount { get; private set; }
    public decimal PenaltyAmount { get; private set; }
    public decimal DiscountAmount { get; private set; }

    public decimal TotalAmount { get; private set; }
    public decimal EndingBalance { get; private set; }

    // Acompanhamento do pagamento.
    public decimal PaidAmount { get; private set; }
    public decimal RemainingAmount { get; private set; }

    public LoanInstallmentStatus Status { get; private set; } = LoanInstallmentStatus.Open;
    public int ScheduleVersion { get; private set; } = 1;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; private set; }
    public int Version { get; private set; }

    private LoanInstallment() { }

    public LoanInstallment(
        Guid contractId,
        Guid userId,
        int installmentNo,
        DateOnly dueDate,
        decimal beginningBalance,
        decimal principalAmount,
        decimal interestAmount,
        decimal totalAmount,
        decimal endingBalance,
        decimal insuranceAmount = 0m,
        decimal feeAmount = 0m,
        int scheduleVersion = 1)
    {
        ContractId = contractId;
        UserId = userId;
        InstallmentNo = installmentNo;
        DueDate = dueDate;
        BeginningBalance = beginningBalance;
        PrincipalAmount = principalAmount;
        InterestAmount = interestAmount;
        InsuranceAmount = insuranceAmount;
        FeeAmount = feeAmount;
        TotalAmount = totalAmount;
        EndingBalance = endingBalance;
        RemainingAmount = totalAmount;
        ScheduleVersion = scheduleVersion;
    }

    public void MarkPaid(DateTime paidAtUtc)
    {
        Status = LoanInstallmentStatus.Paid;
        PaidAt = paidAtUtc;
        PaidAmount = TotalAmount;
        RemainingAmount = 0m;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    /// <summary>Registra o pagamento integral da parcela, aplicando multa/desconto informados.</summary>
    public void RegisterFullPayment(DateTime paidAtUtc, decimal penaltyAmount, decimal discountAmount)
    {
        Status = LoanInstallmentStatus.Paid;
        PaidAt = paidAtUtc.Kind == DateTimeKind.Utc ? paidAtUtc : DateTime.SpecifyKind(paidAtUtc, DateTimeKind.Utc);
        PenaltyAmount = penaltyAmount < 0 ? 0m : penaltyAmount;
        DiscountAmount = discountAmount < 0 ? 0m : discountAmount;
        PaidAmount = TotalAmount + PenaltyAmount - DiscountAmount;
        RemainingAmount = 0m;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    /// <summary>Estorna o pagamento: a parcela volta a ficar em aberto, preservando o histórico do pagamento.</summary>
    public void ReversePayment()
    {
        Status = LoanInstallmentStatus.Open;
        PaidAt = null;
        PenaltyAmount = 0m;
        DiscountAmount = 0m;
        PaidAmount = 0m;
        RemainingAmount = TotalAmount;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }
}
