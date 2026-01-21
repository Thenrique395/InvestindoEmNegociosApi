namespace InvestindoEmNegocio.Domain.Entities;

public class MoneyPayment
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid InstallmentId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime PaidAt { get; private set; }
    public decimal PaidAmount { get; private set; }
    public int? MethodId { get; private set; }
    public string? Note { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private MoneyPayment() { }

    public MoneyPayment(Guid installmentId, Guid userId, DateTime paidAt, decimal paidAmount, int? methodId = null, string? note = null)
    {
        InstallmentId = installmentId;
        UserId = userId;
        PaidAt = paidAt.Kind == DateTimeKind.Utc ? paidAt : DateTime.SpecifyKind(paidAt, DateTimeKind.Utc);
        PaidAmount = paidAmount;
        MethodId = methodId;
        Note = note;
    }
}
