namespace InvestindoEmNegocio.Domain.Entities;

public sealed class BillingWebhookEvent
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Provider { get; private set; } = "stripe";
    public string ProviderEventId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public Guid? UserId { get; private set; }
    public Guid? BillingCheckoutId { get; private set; }
    public bool Processed { get; private set; }
    public bool Success { get; private set; }
    public string PayloadJson { get; private set; } = string.Empty;
    public string? ErrorMessage { get; private set; }
    public DateTime ReceivedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; private set; }

    private BillingWebhookEvent()
    {
    }

    public BillingWebhookEvent(
        string providerEventId,
        string eventType,
        string payloadJson,
        Guid? userId = null,
        Guid? billingCheckoutId = null)
    {
        ProviderEventId = providerEventId.Trim();
        EventType = eventType.Trim();
        PayloadJson = payloadJson;
        UserId = userId;
        BillingCheckoutId = billingCheckoutId;
    }

    public void AttachContext(Guid? userId, Guid? billingCheckoutId)
    {
        UserId ??= userId;
        BillingCheckoutId ??= billingCheckoutId;
    }

    public void MarkProcessed(bool success, string? errorMessage, DateTime nowUtc)
    {
        Processed = true;
        Success = success;
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? null : errorMessage.Trim();
        ProcessedAt = nowUtc;
    }
}
