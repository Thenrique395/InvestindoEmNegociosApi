using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

public sealed class BillingCheckoutCommandService(
    IUserRepository userRepository,
    IUserSubscriptionRepository userSubscriptionRepository,
    IBillingCheckoutRepository billingCheckoutRepository,
    IStripeBillingGateway stripeBillingGateway,
    IBillingNotificationService billingNotificationService,
    ILogger<BillingCheckoutCommandService> logger) : IBillingCheckoutCommandService
{
    public async Task<StartBillingCheckoutResponse> StartCheckoutAsync(Guid userId, StartBillingCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        var plan = SubscriptionPlanCatalog.GetByCodeOrThrow(request.PlanCode);
        if (plan.Role == UserRole.Admin)
            throw new AppProblemException("Plano inválido", "Plano Admin não está disponível para contratação self-service.", StatusCodes.Status400BadRequest);

        if (!Enum.TryParse<SubscriptionBillingCycle>(request.BillingCycle, true, out var cycle))
            throw new AppProblemException("Ciclo de cobrança inválido", "Use Monthly ou Yearly.", StatusCodes.Status400BadRequest);

        if (plan.Code == "basic")
            throw new AppProblemException("Plano gratuito", "O plano Essential não exige checkout de pagamento.", StatusCodes.Status400BadRequest);

        var now = DateTime.UtcNow;
        var amount = cycle == SubscriptionBillingCycle.Yearly ? plan.YearlyPrice : plan.MonthlyPrice;
        var checkout = new BillingCheckout(userId, plan.Code, plan.Role, cycle, amount, "BRL");
        await billingCheckoutRepository.AddAsync(checkout, cancellationToken);
        await billingCheckoutRepository.SaveChangesAsync(cancellationToken);
        var existingSubscription = await userSubscriptionRepository.GetByUserIdAsync(userId, cancellationToken);

        var session = await stripeBillingGateway.CreateCheckoutSessionAsync(
            user,
            checkout,
            plan.Name,
            plan.Description,
            existingSubscription?.ExternalCustomerId,
            cancellationToken);

        checkout.Start(session.Id, session.Url ?? string.Empty, session.ExpiresAt, session.PaymentStatus, now);
        checkout.AttachProviderObjects(session.CustomerId, session.SubscriptionId, session.PaymentIntentId, now);
        await billingCheckoutRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Checkout {CheckoutId} started for user {UserId}, plan {PlanCode} ({BillingCycle}), Stripe session {SessionId}",
            checkout.Id, userId, plan.Code, cycle, session.Id);

        await billingNotificationService.NotifyPendingAsync(user, checkout, cancellationToken);

        return new StartBillingCheckoutResponse(
            checkout.Id,
            checkout.Provider,
            checkout.Status.ToString(),
            checkout.CheckoutUrl ?? string.Empty,
            checkout.PlanCode,
            checkout.BillingCycle.ToString(),
            checkout.Amount,
            checkout.Currency,
            checkout.ExpiresAt);
    }

    private async Task<User> GetUserOrThrowAsync(Guid userId, CancellationToken cancellationToken)
        => await userRepository.GetByIdAsync(userId, cancellationToken)
           ?? throw new AppProblemException("Usuário não encontrado", "Usuário não encontrado.", StatusCodes.Status404NotFound);
}
