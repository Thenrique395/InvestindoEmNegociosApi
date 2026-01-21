using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Entities;

public class MoneyPlan
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public MoneyType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public Guid? CategoryId { get; private set; }
    public Guid? CardId { get; private set; }
    public decimal Amount { get; private set; }
    public ScheduleType Schedule { get; private set; }
    public FrequencyType? Frequency { get; private set; }
    public int? InstallmentsCount { get; private set; }
    public int? DefaultPaymentMethodId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public PlanStatus Status { get; private set; } = PlanStatus.Active;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private MoneyPlan() { }

    public MoneyPlan(Guid userId, MoneyType type, string title, decimal amount, ScheduleType schedule, DateOnly startDate,
        FrequencyType? frequency = null, int? installmentsCount = null, int? defaultPaymentMethodId = null, Guid? categoryId = null, Guid? cardId = null)
    {
        UserId = userId;
        Type = type;
        Title = title.Trim();
        Amount = amount;
        Schedule = schedule;
        StartDate = startDate;
        Frequency = frequency;
        InstallmentsCount = installmentsCount;
        DefaultPaymentMethodId = defaultPaymentMethodId;
        CategoryId = categoryId;
        CardId = cardId;
    }
}
