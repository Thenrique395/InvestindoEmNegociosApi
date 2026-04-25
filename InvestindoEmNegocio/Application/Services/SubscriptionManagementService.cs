using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Application.Services;

public sealed class SubscriptionManagementService(
    IUserRepository userRepository,
    IUserSubscriptionRepository userSubscriptionRepository,
    IUserSessionService userSessionService,
    IStripeBillingGateway stripeBillingGateway,
    IOptions<StripeOptions> stripeOptions) : ISubscriptionManagementService
{
    private readonly StripeOptions _stripeOptions = stripeOptions.Value;

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

        if (!string.IsNullOrWhiteSpace(subscription.ExternalSubscriptionId) && !string.IsNullOrWhiteSpace(_stripeOptions.SecretKey))
        {
            await stripeBillingGateway.ScheduleCancellationAsync(subscription.ExternalSubscriptionId, cancellationToken);
            subscription.ScheduleCancellation(DateTime.UtcNow);
        }
        else
        {
            subscription.CancelNow(DateTime.UtcNow);
            user.SetRole(UserRole.Basic);
            await userRepository.SaveChangesAsync(cancellationToken);
        }

        await userSubscriptionRepository.SaveChangesAsync(cancellationToken);
        var session = await userSessionService.ReissueAsync(user, DateTime.UtcNow, cancellationToken);
        return new SubscriptionChangeResponse(SubscriptionResponseFactory.BuildCurrent(user, subscription), session, SubscriptionResponseFactory.Notes);
    }

    private async Task<User> GetUserOrThrowAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new AppProblemException("Usuário não encontrado", "Usuário não encontrado.", StatusCodes.Status404NotFound);
    }
}
