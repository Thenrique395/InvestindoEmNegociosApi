namespace InvestindoEmNegocio.Application.DTOs;

public sealed record StartBillingCheckoutRequest(string PlanCode, string BillingCycle);

public sealed record StartBillingCheckoutResponse(
    Guid CheckoutId,
    string Provider,
    string Status,
    string CheckoutUrl,
    string PlanCode,
    string BillingCycle,
    decimal Amount,
    string Currency,
    DateTime? ExpiresAt);

public sealed record BillingCheckoutStatusResponse(
    Guid CheckoutId,
    string Provider,
    string Status,
    string PlanCode,
    string BillingCycle,
    decimal Amount,
    string Currency,
    string? ProviderCheckoutId,
    string? ProviderSubscriptionId,
    string? ProviderPaymentStatus,
    bool RequiresLogin,
    bool CanRetry,
    bool CanOpenPortal,
    bool SubscriptionActive,
    bool AutoRenew,
    DateTime? ExpiresAt,
    DateTime? CompletedAt,
    DateTime? RenewsAt,
    DateTime? CancelledAt,
    DateTime? RefundedAt,
    string? FailureReason);

public sealed record BillingPortalSessionResponse(string Url);
