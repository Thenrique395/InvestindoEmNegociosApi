using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Entities;

public class MoneyInstallment
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid PlanId { get; private set; }
    public Guid UserId { get; private set; }
    public int InstallmentNo { get; private set; }
    public DateOnly DueDate { get; private set; }
    public DateOnly? OriginalDueDate { get; private set; }
    public int? StatementYear { get; private set; }
    public int? StatementMonth { get; private set; }
    public DateOnly? StatementCloseDate { get; private set; }
    public DateOnly? StatementDueDate { get; private set; }
    public decimal Amount { get; private set; }
    public InstallmentStatus Status { get; private set; } = InstallmentStatus.Open;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private MoneyInstallment() { }

    public MoneyInstallment(
        Guid planId,
        Guid userId,
        int installmentNo,
        DateOnly dueDate,
        decimal amount,
        DateOnly? originalDueDate = null,
        int? statementYear = null,
        int? statementMonth = null,
        DateOnly? statementCloseDate = null,
        DateOnly? statementDueDate = null)
    {
        PlanId = planId;
        UserId = userId;
        InstallmentNo = installmentNo;
        DueDate = dueDate;
        OriginalDueDate = originalDueDate;
        StatementYear = statementYear;
        StatementMonth = statementMonth;
        StatementCloseDate = statementCloseDate;
        StatementDueDate = statementDueDate;
        Amount = amount;
    }
}
