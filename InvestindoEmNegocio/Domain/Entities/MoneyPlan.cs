using InvestindoEmNegocio.Domain.Common;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Entities;

public class MoneyPlan : ISoftDeletable
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
    public DateTime? DeletedAt { get; private set; }

    private MoneyPlan() { }

    public MoneyPlan(Guid userId, MoneyType type, string title, decimal amount, ScheduleType schedule, DateOnly startDate,
        FrequencyType? frequency = null, int? installmentsCount = null, int? defaultPaymentMethodId = null, Guid? categoryId = null, Guid? cardId = null)
    {
        UserId = userId;
        ApplyDetails(type, title, amount, schedule, startDate, frequency, installmentsCount, defaultPaymentMethodId, categoryId, cardId);
    }

    public void Update(
        MoneyType type,
        string title,
        decimal amount,
        ScheduleType schedule,
        DateOnly startDate,
        FrequencyType? frequency = null,
        int? installmentsCount = null,
        int? defaultPaymentMethodId = null,
        Guid? categoryId = null,
        Guid? cardId = null)
    {
        ApplyDetails(type, title, amount, schedule, startDate, frequency, installmentsCount, defaultPaymentMethodId, categoryId, cardId);
        UpdatedAt = DateTime.UtcNow;
    }

    public void RestoreStatus(PlanStatus status)
    {
        Status = status;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkDeleted(DateTime nowUtc)
    {
        DeletedAt = nowUtc;
        UpdatedAt = nowUtc;
    }

    private void ApplyDetails(
        MoneyType type,
        string title,
        decimal amount,
        ScheduleType schedule,
        DateOnly startDate,
        FrequencyType? frequency,
        int? installmentsCount,
        int? defaultPaymentMethodId,
        Guid? categoryId,
        Guid? cardId)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Título do plano é obrigatório.");
        if (amount <= 0)
            throw new ArgumentException("Valor do plano deve ser maior que zero.");

        var normalizedInstallmentsCount = NormalizeInstallmentsCount(schedule, installmentsCount);
        ValidateSchedule(schedule, frequency, normalizedInstallmentsCount);

        Type = type;
        Title = title.Trim();
        Amount = amount;
        Schedule = schedule;
        StartDate = startDate;
        Frequency = frequency;
        InstallmentsCount = normalizedInstallmentsCount;
        DefaultPaymentMethodId = defaultPaymentMethodId;
        CategoryId = categoryId;
        CardId = cardId;
    }

    private static void ValidateSchedule(ScheduleType schedule, FrequencyType? frequency, int? installmentsCount)
    {
        switch (schedule)
        {
            case ScheduleType.OneTime:
                if (installmentsCount != 1)
                    throw new ArgumentException("ONE_TIME requer installmentsCount = 1.");
                if (frequency is not null)
                    throw new ArgumentException("ONE_TIME não aceita frequency.");
                break;
            case ScheduleType.Installments:
                if (installmentsCount is null || installmentsCount < 2)
                    throw new ArgumentException("INSTALLMENTS requer installmentsCount >= 2.");
                if (frequency is not null)
                    throw new ArgumentException("INSTALLMENTS não aceita frequency.");
                break;
            case ScheduleType.Recurring:
                if (frequency is null)
                    throw new ArgumentException("RECURRING requer frequency.");
                if (installmentsCount is not null)
                    throw new ArgumentException("RECURRING não aceita installmentsCount.");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(schedule), schedule, "Schedule inválido.");
        }
    }

    private static int? NormalizeInstallmentsCount(ScheduleType schedule, int? installmentsCount)
    {
        if (schedule == ScheduleType.OneTime && installmentsCount is null)
            return 1;

        return installmentsCount;
    }
}
