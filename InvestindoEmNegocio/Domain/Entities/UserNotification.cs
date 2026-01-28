using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Entities;

public class UserNotification
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Guid? PlanId { get; private set; }
    public Guid? InstallmentId { get; private set; }
    public MoneyType? MoneyType { get; private set; }
    public NotificationKind Kind { get; private set; }
    public string ReferenceKey { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public DateOnly? DueDate { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; private set; }

    private UserNotification() { }

    public UserNotification(
        Guid userId,
        NotificationKind kind,
        string title,
        string message,
        string referenceKey,
        MoneyType? moneyType = null,
        Guid? planId = null,
        Guid? installmentId = null,
        DateOnly? dueDate = null)
    {
        UserId = userId;
        Kind = kind;
        Title = title.Trim();
        Message = message.Trim();
        ReferenceKey = referenceKey.Trim();
        MoneyType = moneyType;
        PlanId = planId;
        InstallmentId = installmentId;
        DueDate = dueDate;
    }

    public void MarkAsRead()
    {
        if (ReadAt.HasValue) return;
        ReadAt = DateTime.UtcNow;
    }
}
