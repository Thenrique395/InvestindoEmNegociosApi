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
    public string Provider { get; private set; } = "stripe";
    public string? ExternalCustomerId { get; private set; }
    public string? ExternalSubscriptionId { get; private set; }
    public string? ExternalPriceId { get; private set; }
    public DateTime StartedAt { get; private set; } = DateTime.UtcNow;
    public DateTime RenewsAt { get; private set; }
    public bool IsTrial { get; private set; }
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
        Status = UserSubscriptionStatus.PendingActivation;
        StartedAt = startsAtUtc;
        RenewsAt = renewsAtUtc;
    }

    public void Activate(
        string planCode,
        UserRole roleGranted,
        SubscriptionBillingCycle billingCycle,
        decimal priceAmount,
        string currency,
        DateTime nowUtc,
        DateTime renewsAtUtc,
        string? externalCustomerId = null,
        string? externalSubscriptionId = null,
        string? externalPriceId = null)
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
        if (!string.IsNullOrWhiteSpace(externalCustomerId)) ExternalCustomerId = externalCustomerId;
        if (!string.IsNullOrWhiteSpace(externalSubscriptionId)) ExternalSubscriptionId = externalSubscriptionId;
        if (!string.IsNullOrWhiteSpace(externalPriceId)) ExternalPriceId = externalPriceId;
        UpdatedAt = nowUtc;
    }

    public void MarkPendingActivation(
        string planCode,
        UserRole roleGranted,
        SubscriptionBillingCycle billingCycle,
        decimal priceAmount,
        string currency,
        DateTime nowUtc,
        DateTime renewsAtUtc,
        string? externalCustomerId = null,
        string? externalSubscriptionId = null,
        string? externalPriceId = null)
    {
        PlanCode = planCode;
        RoleGranted = roleGranted;
        BillingCycle = billingCycle;
        PriceAmount = priceAmount;
        Currency = currency;
        Status = UserSubscriptionStatus.PendingActivation;
        AutoRenew = true;
        CancelledAt = null;
        RenewsAt = renewsAtUtc;
        if (!string.IsNullOrWhiteSpace(externalCustomerId)) ExternalCustomerId = externalCustomerId;
        if (!string.IsNullOrWhiteSpace(externalSubscriptionId)) ExternalSubscriptionId = externalSubscriptionId;
        if (!string.IsNullOrWhiteSpace(externalPriceId)) ExternalPriceId = externalPriceId;
        UpdatedAt = nowUtc;
    }

    public void SetProvider(string provider)
    {
        if (!string.IsNullOrWhiteSpace(provider))
            Provider = provider.Trim().ToLowerInvariant();
    }

    public void ScheduleCancellation(DateTime nowUtc)
    {
        AutoRenew = false;
        UpdatedAt = nowUtc;
    }

    public void CancelNow(DateTime nowUtc)
    {
        AutoRenew = false;
        Status = UserSubscriptionStatus.Cancelled;
        CancelledAt = nowUtc;
        UpdatedAt = nowUtc;
    }

    public void MarkPastDue(DateTime nowUtc)
    {
        Status = UserSubscriptionStatus.PastDue;
        UpdatedAt = nowUtc;
    }

    public void MarkExpired(DateTime nowUtc)
    {
        Status = UserSubscriptionStatus.Expired;
        AutoRenew = false;
        UpdatedAt = nowUtc;
    }

    public void MarkRefunded(DateTime nowUtc)
    {
        Status = UserSubscriptionStatus.Refunded;
        AutoRenew = false;
        UpdatedAt = nowUtc;
    }

    public void ActivateTrial(string planCode, UserRole roleGranted, DateTime nowUtc, DateTime endsAtUtc)
    {
        PlanCode = planCode;
        RoleGranted = roleGranted;
        Status = UserSubscriptionStatus.Active;
        AutoRenew = false;
        IsTrial = true;
        PriceAmount = 0m;
        StartedAt = nowUtc;
        RenewsAt = endsAtUtc;
        ExternalCustomerId = null;
        ExternalSubscriptionId = null;
        ExternalPriceId = null;
        CancelledAt = null;
        UpdatedAt = nowUtc;
    }
}
