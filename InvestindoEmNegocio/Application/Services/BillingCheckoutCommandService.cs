using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Application.Services;

public sealed class BillingCheckoutCommandService(
    IUserRepository userRepository,
    IUserSubscriptionRepository userSubscriptionRepository,
    IBillingCheckoutRepository billingCheckoutRepository,
    IStripeBillingGateway stripeBillingGateway,
    IMercadoPagoBillingGateway mercadoPagoBillingGateway,
    IOptions<BillingOptions> billingOptions,
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
        var provider = billingOptions.Value.PrimaryProvider;
        checkout.SetProvider(provider);
        await billingCheckoutRepository.AddAsync(checkout, cancellationToken);
        await billingCheckoutRepository.SaveChangesAsync(cancellationToken);

        if (provider == "mercado_pago")
            await StartMercadoPagoCheckoutAsync(user, checkout, plan.Name, cycle, amount, now, cancellationToken);
        else
            await StartStripeCheckoutAsync(user, checkout, plan.Name, plan.Description, now, cancellationToken);

        await billingCheckoutRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Checkout {CheckoutId} started for user {UserId}, plan {PlanCode} ({BillingCycle}), provider {Provider}",
            checkout.Id, userId, plan.Code, cycle, checkout.Provider);

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

    private async Task StartStripeCheckoutAsync(User user, BillingCheckout checkout, string planName, string planDescription, DateTime now, CancellationToken cancellationToken)
    {
        var existingSubscription = await userSubscriptionRepository.GetByUserIdAsync(checkout.UserId, cancellationToken);

        var session = await stripeBillingGateway.CreateCheckoutSessionAsync(
            user,
            checkout,
            planName,
            planDescription,
            existingSubscription?.ExternalCustomerId,
            cancellationToken);

        checkout.Start(session.Id, session.Url ?? string.Empty, session.ExpiresAt, session.PaymentStatus, now);
        checkout.AttachProviderObjects(session.CustomerId, session.SubscriptionId, session.PaymentIntentId, now);
    }

    private async Task StartMercadoPagoCheckoutAsync(User user, BillingCheckout checkout, string planName, SubscriptionBillingCycle cycle, decimal amount, DateTime now, CancellationToken cancellationToken)
    {
        var preapproval = await mercadoPagoBillingGateway.CreatePreapprovalAsync(
            $"Investindo em Negócios - {planName}",
            user.Email,
            checkout.Id.ToString(),
            amount,
            checkout.Currency,
            cycle,
            cancellationToken);

        checkout.Start(preapproval.Id, preapproval.InitPoint ?? string.Empty, null, preapproval.Status, now);
        checkout.AttachProviderObjects(null, preapproval.Id, null, now);
    }

    private async Task<User> GetUserOrThrowAsync(Guid userId, CancellationToken cancellationToken)
        => await userRepository.GetByIdAsync(userId, cancellationToken)
           ?? throw new AppProblemException("Usuário não encontrado", "Usuário não encontrado.", StatusCodes.Status404NotFound);
}
