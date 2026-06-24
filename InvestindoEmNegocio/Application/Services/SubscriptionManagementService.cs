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

public sealed class SubscriptionManagementService(
    IUserRepository userRepository,
    IUserSubscriptionRepository userSubscriptionRepository,
    IBillingCheckoutRepository billingCheckoutRepository,
    IUserSessionService userSessionService,
    IStripeBillingGateway stripeBillingGateway,
    IMercadoPagoBillingGateway mercadoPagoBillingGateway,
    IOptions<StripeOptions> stripeOptions,
    ILogger<SubscriptionManagementService> logger) : ISubscriptionManagementService
{
    private readonly StripeOptions _stripeOptions = stripeOptions.Value;
    private const string MercadoPagoProvider = "mercado_pago";

    public async Task<SubscriptionChangeResponse> ChangeAsync(Guid userId, ChangeSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        if (user.Role == UserRole.Admin)
        {
            throw new AppProblemException("Plano gerenciado manualmente", "Usuários Admin não podem alterar o plano via self-service.", StatusCodes.Status400BadRequest);
        }

        var plan = SubscriptionPlanCatalog.GetByCodeOrThrow(request.PlanCode);
        if (plan.Role == UserRole.Admin)
        {
            throw new AppProblemException("Plano inválido", "O plano solicitado não está disponível via self-service.", StatusCodes.Status400BadRequest);
        }

        if (!Enum.TryParse<SubscriptionBillingCycle>(request.BillingCycle, true, out var cycle))
        {
            throw new AppProblemException("Ciclo de cobrança inválido", "Use Monthly ou Yearly.", StatusCodes.Status400BadRequest);
        }

        var now = DateTime.UtcNow;
        var renewsAt = cycle == SubscriptionBillingCycle.Yearly ? now.AddYears(1) : now.AddMonths(1);
        var price = cycle == SubscriptionBillingCycle.Yearly ? plan.YearlyPrice : plan.MonthlyPrice;

        var subscription = await userSubscriptionRepository.GetByUserIdAsync(userId, cancellationToken);
        if (plan.Code != "basic")
        {
            throw new AppProblemException(
                "Payment required",
                "Paid plans must be purchased through billing checkout before activation.",
                StatusCodes.Status402PaymentRequired);
        }

        if (subscription is null)
        {
            subscription = new UserSubscription(userId, plan.Code, plan.Role, cycle, price, "BRL", now, renewsAt);
            await userSubscriptionRepository.AddAsync(subscription, cancellationToken);
        }

        subscription.Activate(plan.Code, plan.Role, cycle, price, "BRL", now, renewsAt);
        user.SetRole(plan.Role);
        await userRepository.SaveChangesAsync(cancellationToken);
        await userSubscriptionRepository.SaveChangesAsync(cancellationToken);

        var session = await userSessionService.ReissueAsync(user, now, cancellationToken);
        return new SubscriptionChangeResponse(SubscriptionResponseFactory.BuildCurrent(user, subscription), session, SubscriptionResponseFactory.Notes);
    }

    public async Task<SubscriptionChangeResponse> CancelAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        if (user.Role == UserRole.Admin)
        {
            throw new AppProblemException("Plano gerenciado manualmente", "Usuários Admin não podem cancelar o plano via self-service.", StatusCodes.Status400BadRequest);
        }

        var subscription = await userSubscriptionRepository.GetByUserIdAsync(userId, cancellationToken);
        if (subscription is null)
        {
            subscription = new UserSubscription(userId, "basic", UserRole.Basic, SubscriptionBillingCycle.Monthly, 0m, "BRL", DateTime.UtcNow, DateTime.UtcNow);
            await userSubscriptionRepository.AddAsync(subscription, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(subscription.ExternalSubscriptionId) && subscription.Provider == MercadoPagoProvider)
        {
            // MP não tem "cancelar no fim do período" nativo — cancela a cobrança recorrente
            // já agora para não continuar debitando o usuário; o acesso local continua até
            // RenewsAt via ScheduleCancellation, igual ao comportamento do Stripe para o usuário.
            await mercadoPagoBillingGateway.CancelAsync(subscription.ExternalSubscriptionId, cancellationToken);
            subscription.ScheduleCancellation(DateTime.UtcNow);
        }
        else if (!string.IsNullOrWhiteSpace(subscription.ExternalSubscriptionId) && !string.IsNullOrWhiteSpace(_stripeOptions.SecretKey))
        {
            await stripeBillingGateway.ScheduleCancellationAsync(subscription.ExternalSubscriptionId, cancellationToken);
            subscription.ScheduleCancellation(DateTime.UtcNow);
        }
        else
        {
            subscription.ScheduleCancellation(DateTime.UtcNow);
        }

        await userSubscriptionRepository.SaveChangesAsync(cancellationToken);
        var session = await userSessionService.ReissueAsync(user, DateTime.UtcNow, cancellationToken);
        return new SubscriptionChangeResponse(SubscriptionResponseFactory.BuildCurrent(user, subscription), session, SubscriptionResponseFactory.Notes);
    }

    public async Task<SubscriptionChangeResponse> RequestRefundAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        if (user.Role == UserRole.Admin)
            throw new AppProblemException("Ação indisponível", "Usuários Admin não podem solicitar reembolso.", StatusCodes.Status400BadRequest);

        var subscription = await userSubscriptionRepository.GetByUserIdAsync(userId, cancellationToken);
        if (subscription is null || subscription.PlanCode == "basic" || subscription.Status == UserSubscriptionStatus.Refunded)
            throw new AppProblemException("Sem assinatura paga", "Não há assinatura ativa elegível para reembolso.", StatusCodes.Status400BadRequest);

        var now = DateTime.UtcNow;
        var graceDays = 7;
        if ((now - subscription.StartedAt).TotalDays > graceDays)
            throw new AppProblemException("Prazo expirado", $"O direito de arrependimento é válido por {graceDays} dias após a contratação.", StatusCodes.Status400BadRequest);

        if (subscription.Provider == MercadoPagoProvider && !string.IsNullOrWhiteSpace(subscription.ExternalSubscriptionId))
        {
            // Cancela a cobrança recorrente imediatamente (direito de não ser cobrado de novo).
            await mercadoPagoBillingGateway.CancelAsync(subscription.ExternalSubscriptionId, cancellationToken);

            var checkout = await billingCheckoutRepository.GetByProviderSubscriptionIdAsync(subscription.ExternalSubscriptionId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(checkout?.ProviderPaymentIntentId))
            {
                await mercadoPagoBillingGateway.RefundPaymentAsync(checkout.ProviderPaymentIntentId, cancellationToken);
            }
            else
            {
                // Sem pagamento vinculado (webhook "payment" do MP ainda não chegou, ou o
                // external_reference não correspondeu) — precisa de reconciliação manual.
                logger.LogWarning(
                    "RequestRefundAsync: assinatura {SubscriptionId} (user {UserId}) é Mercado Pago — preapproval cancelado, mas não há pagamento vinculado para reembolsar automaticamente.",
                    subscription.ExternalSubscriptionId, userId);
            }
        }
        else if (!string.IsNullOrWhiteSpace(subscription.ExternalSubscriptionId) && !string.IsNullOrWhiteSpace(_stripeOptions.SecretKey))
        {
            await stripeBillingGateway.CancelImmediatelyAsync(subscription.ExternalSubscriptionId, cancellationToken);

            var checkout = await billingCheckoutRepository.GetByProviderSubscriptionIdAsync(subscription.ExternalSubscriptionId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(checkout?.ProviderPaymentIntentId))
                await stripeBillingGateway.RefundPaymentIntentAsync(checkout.ProviderPaymentIntentId, cancellationToken);
        }

        subscription.MarkRefunded(now);
        user.SetRole(UserRole.Basic);
        await userRepository.SaveChangesAsync(cancellationToken);
        await userSubscriptionRepository.SaveChangesAsync(cancellationToken);

        var session = await userSessionService.ReissueAsync(user, now, cancellationToken);
        return new SubscriptionChangeResponse(SubscriptionResponseFactory.BuildCurrent(user, subscription), session, SubscriptionResponseFactory.Notes);
    }

    public async Task<SubscriptionChangeResponse> RequestTrialAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        if (user.Role == UserRole.Admin)
            throw new AppProblemException("Ação indisponível", "Usuários Admin não precisam de período de teste.", StatusCodes.Status400BadRequest);

        if (user.TrialUsedAt.HasValue)
            throw new AppProblemException("Trial já utilizado", "Sua conta já utilizou o período de teste gratuito.", StatusCodes.Status400BadRequest);

        var subscription = await userSubscriptionRepository.GetByUserIdAsync(userId, cancellationToken);
        if (subscription is not null && subscription.Status == UserSubscriptionStatus.Active && !subscription.IsTrial)
            throw new AppProblemException("Assinatura ativa", "Você já possui uma assinatura ativa. O período de teste é para contas sem plano.", StatusCodes.Status400BadRequest);

        var now = DateTime.UtcNow;
        var trialPlan = SubscriptionPlanCatalog.GetByCodeOrThrow("advanced");
        var trialDays = 7;

        if (subscription is null)
        {
            subscription = new UserSubscription(userId, trialPlan.Code, trialPlan.Role, SubscriptionBillingCycle.Monthly, 0m, "BRL", now, now.AddDays(trialDays));
            await userSubscriptionRepository.AddAsync(subscription, cancellationToken);
        }

        subscription.ActivateTrial(trialPlan.Code, trialPlan.Role, now, now.AddDays(trialDays));
        user.SetRole(trialPlan.Role);
        user.MarkTrialUsed(now);
        await userRepository.SaveChangesAsync(cancellationToken);
        await userSubscriptionRepository.SaveChangesAsync(cancellationToken);

        var session = await userSessionService.ReissueAsync(user, now, cancellationToken);
        return new SubscriptionChangeResponse(SubscriptionResponseFactory.BuildCurrent(user, subscription), session, SubscriptionResponseFactory.Notes);
    }

    public async Task RetryPaymentAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        if (user.Role == UserRole.Admin)
            throw new AppProblemException("Ação indisponível", "Usuários Admin não precisam de retry de pagamento.", StatusCodes.Status400BadRequest);

        var subscription = await userSubscriptionRepository.GetByUserIdAsync(userId, cancellationToken);
        if (subscription is null || subscription.Status != UserSubscriptionStatus.PastDue)
            throw new AppProblemException("Retry indisponível", "Só é possível tentar novamente quando há pagamento em atraso.", StatusCodes.Status400BadRequest);

        if (string.IsNullOrWhiteSpace(subscription.ExternalSubscriptionId))
            throw new AppProblemException("Retry indisponível", "Assinatura sem vínculo com provedor de pagamento.", StatusCodes.Status400BadRequest);

        if (subscription.Provider == MercadoPagoProvider)
            throw new AppProblemException("Retry indisponível", "Nova tentativa de cobrança ainda não está disponível para assinaturas Mercado Pago.", StatusCodes.Status501NotImplemented);

        if (string.IsNullOrWhiteSpace(_stripeOptions.SecretKey))
            throw new AppProblemException("Cobrança indisponível", "Stripe não está configurado neste ambiente.", StatusCodes.Status503ServiceUnavailable);

        var invoice = await stripeBillingGateway.GetLatestOpenInvoiceAsync(subscription.ExternalSubscriptionId, cancellationToken);
        if (invoice is null)
            throw new AppProblemException("Fatura não encontrada", "Não há fatura em aberto para esta assinatura.", StatusCodes.Status404NotFound);

        await stripeBillingGateway.PayInvoiceAsync(invoice.Id, cancellationToken);
    }

    private async Task<User> GetUserOrThrowAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new AppProblemException("Usuário não encontrado", "Usuário não encontrado.", StatusCodes.Status404NotFound);
    }
}
