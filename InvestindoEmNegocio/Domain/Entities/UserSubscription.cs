using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Entities;

public class UserSubscription
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string PlanCode { get; private set; }
    public UserRole RoleGranted { get; private set; }
    public SubscriptionBillingCycle BillingCycle { get; private set; }
    public UserSubscriptionStatus Status { get; private set; } = UserSubscriptionStatus.Active;
    public decimal PriceAmount { get; private set; }
    public string Currency { get; private set; }
    public bool AutoRenew { get; private set; } = true;
    public DateTime StartedAt { get; private set; } = DateTime.UtcNow;
    public DateTime RenewsAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private UserSubscription()
    {
        PlanCode = string.Empty;
        Currency = "BRL";
    }

    public UserSubscription(
        Guid userId,
        string planCode,
        UserRole roleGranted,
        SubscriptionBillingCycle billingCycle,
        decimal priceAmount,
        string currency,
        DateTime startsAtUtc,
        DateTime renewsAtUtc)
    {
        UserId = userId;
        PlanCode = planCode;
        RoleGranted = roleGranted;
        BillingCycle = billingCycle;
        PriceAmount = priceAmount;
        Currency = currency;
        StartedAt = startsAtUtc;
        RenewsAt = renewsAtUtc;
    }

    public void ChangePlan(
        string planCode,
        UserRole roleGranted,
        SubscriptionBillingCycle billingCycle,
        decimal priceAmount,
        string currency,
        DateTime nowUtc,
        DateTime renewsAtUtc)
    {
        PlanCode = planCode;
        RoleGranted = roleGranted;
        BillingCycle = billingCycle;
        PriceAmount = priceAmount;
        Currency = currency;
        Status = UserSubscriptionStatus.Active;
        AutoRenew = true;
        CancelledAt = null;
        RenewsAt = renewsAtUtc;
        UpdatedAt = nowUtc;
    }

    public void CancelAutoRenew(DateTime nowUtc)
    {
        AutoRenew = false;
        Status = UserSubscriptionStatus.Cancelled;
        CancelledAt = nowUtc;
        UpdatedAt = nowUtc;
    }
}
