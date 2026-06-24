using InvestindoEmNegocio.Application.Interfaces;
using Stripe;

namespace InvestindoEmNegocio.Application.Services;

internal static class StripeSubscriptionMapping
{
    public static ProviderSubscriptionSnapshot ToProviderSnapshot(this Subscription subscription) => new(
        subscription.Id,
        subscription.CustomerId,
        subscription.Status,
        subscription.Items?.Data?.FirstOrDefault()?.Price?.Id,
        ResolveCurrentPeriodEnd(subscription),
        subscription.CancelAtPeriodEnd,
        subscription.Metadata);

    private static DateTime? ResolveCurrentPeriodEnd(Subscription subscription)
    {
        var currentPeriodEnd = subscription.Items?.Data?.FirstOrDefault()?.CurrentPeriodEnd;
        return currentPeriodEnd is DateTime dt && dt > DateTime.MinValue ? dt.ToUniversalTime() : null;
    }
}
