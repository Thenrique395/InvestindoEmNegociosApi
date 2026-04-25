using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.Services;

internal static class SubscriptionResponseFactory
{
    internal static readonly IReadOnlyList<string> Notes =
    [
        "Planos pagos só ativam o acesso após confirmação do pagamento.",
        "Plano Admin não é vendido por self-service.",
        "Cancelamento agenda o fim da renovação automática no gateway de pagamento.",
        "Falhas de pagamento e renovação são sincronizadas por webhook."
    ];

    internal static CurrentSubscriptionResponse BuildCurrent(User user, UserSubscription? subscription)
    {
        if (subscription is null)
        {
            return new CurrentSubscriptionResponse(
                "basic",
                "Basic",
                user.Role.ToString(),
                "Active",
                SubscriptionBillingCycle.Monthly.ToString(),
                0m,
                "BRL",
                false,
                user.CreatedAt,
                null,
                null);
        }

        var plan = SubscriptionPlanCatalog.GetByCodeOrThrow(subscription.PlanCode);
        return new CurrentSubscriptionResponse(
            subscription.PlanCode,
            plan.Name,
            user.Role.ToString(),
            subscription.Status.ToString(),
            subscription.BillingCycle.ToString(),
            subscription.PriceAmount,
            subscription.Currency,
            subscription.AutoRenew,
            subscription.StartedAt,
            subscription.RenewsAt,
            subscription.CancelledAt);
    }
}
