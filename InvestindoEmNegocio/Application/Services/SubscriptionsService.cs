using System.Security.Cryptography;
using System.Text;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;

namespace InvestindoEmNegocio.Application.Services;

public sealed class SubscriptionsService(
    IUserRepository userRepository,
    IUserSubscriptionRepository userSubscriptionRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IJwtTokenGenerator jwtTokenGenerator) : ISubscriptionsService
{
    private static readonly IReadOnlyList<string> Notes =
    [
        "A mudança de plano atualiza o role do usuário e renova a sessão imediatamente.",
        "Plano Admin não é vendido por self-service.",
        "Cancelamento interrompe renovação automática e retorna para Basic ao fim da vigência."
    ];

    public async Task<SubscriptionCatalogResponse> GetCatalogAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        var subscription = await userSubscriptionRepository.GetByUserIdAsync(userId, cancellationToken);
        var current = BuildCurrent(user, subscription);
        var plans = SubscriptionPlanCatalog.Plans
            .Select(plan => new SubscriptionPlanResponse(
                plan.Code,
                plan.Name,
                plan.Role.ToString(),
                plan.Description,
                plan.MonthlyPrice,
                plan.YearlyPrice,
                plan.Recommended,
                string.Equals(plan.Code, current.PlanCode, StringComparison.OrdinalIgnoreCase),
                plan.Features,
                plan.Limits))
            .ToList();

        return new SubscriptionCatalogResponse(current, plans, Notes);
    }

    public async Task<SubscriptionChangeResponse> ChangeAsync(Guid userId, ChangeSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        if (user.Role == UserRole.Admin)
        {
            throw new AppProblemException("Plano administrado manualmente", "Usuários Admin não podem alterar o plano por self-service.", StatusCodes.Status400BadRequest);
        }

        var plan = SubscriptionPlanCatalog.GetByCodeOrThrow(request.PlanCode);
        if (plan.Role == UserRole.Admin)
        {
            throw new AppProblemException("Plano inválido", "O plano solicitado não está disponível em self-service.", StatusCodes.Status400BadRequest);
        }

        if (!Enum.TryParse<SubscriptionBillingCycle>(request.BillingCycle, true, out var cycle))
        {
            throw new AppProblemException("Ciclo inválido", "Informe Monthly ou Yearly.", StatusCodes.Status400BadRequest);
        }

        var now = DateTime.UtcNow;
        var renewsAt = cycle == SubscriptionBillingCycle.Yearly ? now.AddYears(1) : now.AddMonths(1);
        var price = cycle == SubscriptionBillingCycle.Yearly ? plan.YearlyPrice : plan.MonthlyPrice;

        var subscription = await userSubscriptionRepository.GetByUserIdAsync(userId, cancellationToken);
        if (subscription is null)
        {
            subscription = new UserSubscription(userId, plan.Code, plan.Role, cycle, price, "BRL", now, renewsAt);
            await userSubscriptionRepository.AddAsync(subscription, cancellationToken);
        }
        else
        {
            subscription.ChangePlan(plan.Code, plan.Role, cycle, price, "BRL", now, renewsAt);
        }

        user.SetRole(plan.Role);
        await userRepository.SaveChangesAsync(cancellationToken);
        await userSubscriptionRepository.SaveChangesAsync(cancellationToken);

        var session = await ReissueSessionAsync(user, now, cancellationToken);
        return new SubscriptionChangeResponse(BuildCurrent(user, subscription), session, Notes);
    }

    public async Task<SubscriptionChangeResponse> CancelAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        if (user.Role == UserRole.Admin)
        {
            throw new AppProblemException("Plano administrado manualmente", "Usuários Admin não podem cancelar o plano por self-service.", StatusCodes.Status400BadRequest);
        }

        var subscription = await userSubscriptionRepository.GetByUserIdAsync(userId, cancellationToken);
        if (subscription is null)
        {
            subscription = new UserSubscription(userId, "basic", UserRole.Basic, SubscriptionBillingCycle.Monthly, 0m, "BRL", DateTime.UtcNow, DateTime.UtcNow);
            await userSubscriptionRepository.AddAsync(subscription, cancellationToken);
        }

        subscription.CancelAutoRenew(DateTime.UtcNow);
        user.SetRole(UserRole.Basic);
        await userRepository.SaveChangesAsync(cancellationToken);
        await userSubscriptionRepository.SaveChangesAsync(cancellationToken);
        var session = await ReissueSessionAsync(user, DateTime.UtcNow, cancellationToken);
        return new SubscriptionChangeResponse(BuildCurrent(user, subscription), session, Notes);
    }

    private async Task<User> GetUserOrThrowAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new AppProblemException("Usuário não encontrado", "Usuário não encontrado.", StatusCodes.Status404NotFound);
    }

    private CurrentSubscriptionResponse BuildCurrent(User user, UserSubscription? subscription)
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

    private async Task<AuthResponse> ReissueSessionAsync(User user, DateTime nowUtc, CancellationToken cancellationToken)
    {
        await refreshTokenRepository.RevokeActiveByUserAsync(user.Id, nowUtc, cancellationToken);
        var access = jwtTokenGenerator.Generate(user);
        var rawRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var hashedRefreshToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawRefreshToken)));
        var refreshEntity = new RefreshToken(user.Id, hashedRefreshToken, nowUtc.AddDays(30));
        await refreshTokenRepository.AddAsync(refreshEntity, cancellationToken);
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return new AuthResponse(user.Id, user.Name, user.Email, user.Role.ToString(), access.Token, rawRefreshToken, access.ExpiresAt);
    }
}
