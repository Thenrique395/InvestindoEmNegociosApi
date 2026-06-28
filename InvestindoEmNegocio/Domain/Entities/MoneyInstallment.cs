using InvestindoEmNegocio.Domain.Common;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Entities;

public class MoneyInstallment : ISoftDeletable
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid PlanId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid SpaceId { get; private set; }
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
    public DateTime? DeletedAt { get; private set; }

    private MoneyInstallment() { }

    public MoneyInstallment(
        Guid planId,
        Guid userId,
        Guid spaceId,
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
        SpaceId = spaceId;
        InstallmentNo = installmentNo;
        DueDate = dueDate;
        OriginalDueDate = originalDueDate;
        StatementYear = statementYear;
        StatementMonth = statementMonth;
        StatementCloseDate = statementCloseDate;
        StatementDueDate = statementDueDate;
        Amount = amount;
    }

    public void Anticipate(DateOnly dueDate, DateOnly today)
    {
        if (DueDate.Year == today.Year && DueDate.Month == today.Month)
            throw new InvalidOperationException("Não é possível antecipar parcelas do mês atual.");

        if (OriginalDueDate is null)
            OriginalDueDate = DueDate;

        DueDate = dueDate;
        Status = InstallmentStatus.Anticipated;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RefreshPaymentStatus(decimal totalPaid)
    {
        if (totalPaid <= 0)
            Status = InstallmentStatus.Open;
        else if (totalPaid < Amount)
            Status = InstallmentStatus.PartiallyPaid;
        else
            Status = InstallmentStatus.Paid;

        UpdatedAt = DateTime.UtcNow;
    }

    public void RestoreStatus(InstallmentStatus status)
    {
        Status = status;
        UpdatedAt = DateTime.UtcNow;
    }

    internal void RestoreCreatedAt(DateTime createdAt)
    {
        CreatedAt = createdAt.Kind == DateTimeKind.Utc
            ? createdAt
            : DateTime.SpecifyKind(createdAt, DateTimeKind.Utc);
    }

    public void MarkDeleted(DateTime nowUtc)
    {
        DeletedAt = nowUtc;
        UpdatedAt = nowUtc;
    }
}
