using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Entities;

public sealed class BillingCheckout
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string Provider { get; private set; } = "stripe";
    public string PlanCode { get; private set; } = string.Empty;
    public UserRole RoleRequested { get; private set; }
    public SubscriptionBillingCycle BillingCycle { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "BRL";
    public BillingCheckoutStatus Status { get; private set; } = BillingCheckoutStatus.Draft;
    public string? ProviderCheckoutId { get; private set; }
    public string? ProviderCustomerId { get; private set; }
    public string? ProviderSubscriptionId { get; private set; }
    public string? ProviderPaymentIntentId { get; private set; }
    public string? CheckoutUrl { get; private set; }
    public string? ProviderPaymentStatus { get; private set; }
    public string? LastProviderEventType { get; private set; }
    public string? FailureReason { get; private set; }
    public bool EmailSuccessSent { get; private set; }
    public bool EmailPendingSent { get; private set; }
    public bool EmailFailureSent { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? RefundedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private BillingCheckout()
    {
    }

    public BillingCheckout(
        Guid userId,
        string planCode,
        UserRole roleRequested,
        SubscriptionBillingCycle billingCycle,
        decimal amount,
        string currency)
    {
        UserId = userId;
        PlanCode = planCode.Trim().ToLowerInvariant();
        RoleRequested = roleRequested;
        BillingCycle = billingCycle;
        Amount = amount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "BRL" : currency.Trim().ToUpperInvariant();
    }

    public void Start(
        string providerCheckoutId,
        string checkoutUrl,
        DateTime? expiresAtUtc,
        string? providerPaymentStatus,
        DateTime nowUtc)
    {
        ProviderCheckoutId = providerCheckoutId;
        CheckoutUrl = checkoutUrl;
        ExpiresAt = expiresAtUtc;
        ProviderPaymentStatus = providerPaymentStatus;
        Status = BillingCheckoutStatus.Pending;
        FailureReason = null;
        UpdatedAt = nowUtc;
    }

    public void AttachProviderObjects(
        string? customerId,
        string? subscriptionId,
        string? paymentIntentId,
        DateTime nowUtc)
    {
        if (!string.IsNullOrWhiteSpace(customerId)) ProviderCustomerId = customerId;
        if (!string.IsNullOrWhiteSpace(subscriptionId)) ProviderSubscriptionId = subscriptionId;
        if (!string.IsNullOrWhiteSpace(paymentIntentId)) ProviderPaymentIntentId = paymentIntentId;
        UpdatedAt = nowUtc;
    }

    public void MarkPending(string? providerStatus, string? eventType, DateTime nowUtc)
    {
        Status = BillingCheckoutStatus.Pending;
        ProviderPaymentStatus = providerStatus;
        LastProviderEventType = eventType;
        UpdatedAt = nowUtc;
    }

    public void MarkRequiresAction(string? providerStatus, string? eventType, DateTime nowUtc)
    {
        Status = BillingCheckoutStatus.RequiresAction;
        ProviderPaymentStatus = providerStatus;
        LastProviderEventType = eventType;
        UpdatedAt = nowUtc;
    }

    public void MarkPaid(string? providerStatus, string? eventType, DateTime nowUtc)
    {
        Status = BillingCheckoutStatus.Paid;
        ProviderPaymentStatus = providerStatus;
        LastProviderEventType = eventType;
        CompletedAt ??= nowUtc;
        FailureReason = null;
        UpdatedAt = nowUtc;
    }

    public void MarkFailed(string? reason, string? providerStatus, string? eventType, DateTime nowUtc)
    {
        Status = BillingCheckoutStatus.Failed;
        FailureReason = reason;
        ProviderPaymentStatus = providerStatus;
        LastProviderEventType = eventType;
        UpdatedAt = nowUtc;
    }

    public void MarkExpired(string? eventType, DateTime nowUtc)
    {
        Status = BillingCheckoutStatus.Expired;
        LastProviderEventType = eventType;
        UpdatedAt = nowUtc;
    }

    public void MarkRefunded(string? eventType, DateTime nowUtc)
    {
        Status = BillingCheckoutStatus.Refunded;
        LastProviderEventType = eventType;
        RefundedAt ??= nowUtc;
        UpdatedAt = nowUtc;
    }

    public void MarkCancelled(string? eventType, DateTime nowUtc)
    {
        Status = BillingCheckoutStatus.Cancelled;
        LastProviderEventType = eventType;
        CancelledAt ??= nowUtc;
        UpdatedAt = nowUtc;
    }

    public void MarkSuccessEmailSent(DateTime nowUtc)
    {
        EmailSuccessSent = true;
        UpdatedAt = nowUtc;
    }

    public void MarkPendingEmailSent(DateTime nowUtc)
    {
        EmailPendingSent = true;
        UpdatedAt = nowUtc;
    }

    public void MarkFailureEmailSent(DateTime nowUtc)
    {
        EmailFailureSent = true;
        UpdatedAt = nowUtc;
    }
}
