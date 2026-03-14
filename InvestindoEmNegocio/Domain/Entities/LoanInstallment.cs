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
    public decimal TotalAmount { get; private set; }
    public decimal EndingBalance { get; private set; }
    public LoanInstallmentStatus Status { get; private set; } = LoanInstallmentStatus.Open;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; private set; }

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
        decimal endingBalance)
    {
        ContractId = contractId;
        UserId = userId;
        InstallmentNo = installmentNo;
        DueDate = dueDate;
        BeginningBalance = beginningBalance;
        PrincipalAmount = principalAmount;
        InterestAmount = interestAmount;
        TotalAmount = totalAmount;
        EndingBalance = endingBalance;
    }

    public void MarkPaid(DateTime paidAtUtc)
    {
        Status = LoanInstallmentStatus.Paid;
        PaidAt = paidAtUtc;
        UpdatedAt = DateTime.UtcNow;
    }
}
