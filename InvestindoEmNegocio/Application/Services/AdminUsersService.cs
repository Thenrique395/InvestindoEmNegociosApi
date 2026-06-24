using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Domain.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace InvestindoEmNegocio.Application.Services;

public sealed class AdminUsersService(
    IUserRepository userRepository,
    IUserSubscriptionRepository userSubscriptionRepository,
    IUserFeatureOverrideRepository featureOverrideRepository,
    IUserSessionService userSessionService,
    IAuditService auditService) : IAdminUsersService
{
    public async Task<IReadOnlyList<UserSummaryResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var users = await userRepository.ListAsync(cancellationToken);
        return users
            .Select(u => new UserSummaryResponse(u.Id, u.Name, u.Email, u.Role.ToString(), u.IsActive, u.CreatedAt))
            .ToList();
    }

    public async Task<UserSummaryResponse> UpdateRoleAsync(Guid id, string role, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<UserRole>(role, true, out var parsedRole))
        {
            throw new AppProblemException("Função inválida", "A função informada não é válida.", StatusCodes.Status400BadRequest);
        }

        var user = await userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppProblemException("Usuário não encontrado", "Usuário não encontrado.", StatusCodes.Status404NotFound);

        user.SetRole(parsedRole);
        try
        {
            await userRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new AppProblemException(
                "Falha ao salvar",
                "Não foi possível atualizar o usuário no momento.",
                StatusCodes.Status409Conflict);
        }

        return new UserSummaryResponse(user.Id, user.Name, user.Email, user.Role.ToString(), user.IsActive, user.CreatedAt);
    }

    public async Task<UserSummaryResponse> UpdateStatusAsync(Guid id, bool isActive, Guid currentUserId, CancellationToken cancellationToken)
    {
        if (id == currentUserId && !isActive)
        {
            throw new AppProblemException("Ação inválida", "Você não pode bloquear o próprio acesso.", StatusCodes.Status400BadRequest);
        }

        var user = await userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppProblemException("Usuário não encontrado", "Usuário não encontrado.", StatusCodes.Status404NotFound);

        if (isActive)
        {
            user.Activate();
        }
        else
        {
            // Desativação tem que matar sessões já emitidas, não só bloquear logins novos —
            // senão o token/refresh token que o usuário já tem continua funcionando.
            user.Deactivate();
            user.RevokeSessions();
        }

        try
        {
            await userRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new AppProblemException(
                "Falha ao salvar",
                "Não foi possível atualizar o usuário no momento.",
                StatusCodes.Status409Conflict);
        }

        if (!isActive)
        {
            await userSessionService.RevokeActiveAsync(user.Id, DateTime.UtcNow, cancellationToken);
        }

        return new UserSummaryResponse(user.Id, user.Name, user.Email, user.Role.ToString(), user.IsActive, user.CreatedAt);
    }

    public async Task<IReadOnlyList<UserFeatureAccessResponse>> ListFeaturesAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppProblemException("Usuário não encontrado", "Usuário não encontrado.", StatusCodes.Status404NotFound);

        var overrides = await featureOverrideRepository.ListByUserAsync(id, cancellationToken);
        return BuildFeatureAccessResponse(user.Role, overrides);
    }

    public async Task<IReadOnlyList<UserFeatureAccessResponse>> SetFeatureOverrideAsync(
        Guid id,
        string featureKey,
        bool isEnabled,
        Guid executorUserId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var normalizedFeature = NormalizeFeatureOrThrow(featureKey);
        var user = await userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppProblemException("Usuário não encontrado", "Usuário não encontrado.", StatusCodes.Status404NotFound);

        var existing = await featureOverrideRepository.GetByUserAndFeatureAsync(id, normalizedFeature, cancellationToken);
        bool? previousValue = existing?.IsEnabled;
        if (existing is null)
        {
            await featureOverrideRepository.AddAsync(new UserFeatureOverride(id, normalizedFeature, isEnabled), cancellationToken);
        }
        else
        {
            existing.SetEnabled(isEnabled);
        }

        await featureOverrideRepository.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(
            executorUserId,
            "SET_FEATURE_OVERRIDE",
            "UserFeatureOverride",
            id.ToString(),
            ipAddress,
            userAgent,
            BuildSetOverrideMetadata(id, normalizedFeature, previousValue, isEnabled),
            cancellationToken);

        var overrides = await featureOverrideRepository.ListByUserAsync(id, cancellationToken);
        return BuildFeatureAccessResponse(user.Role, overrides);
    }

    public async Task<IReadOnlyList<UserFeatureAccessResponse>> ClearFeatureOverrideAsync(
        Guid id,
        string featureKey,
        Guid executorUserId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var normalizedFeature = NormalizeFeatureOrThrow(featureKey);
        var user = await userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppProblemException("Usuário não encontrado", "Usuário não encontrado.", StatusCodes.Status404NotFound);

        var existing = await featureOverrideRepository.GetByUserAndFeatureAsync(id, normalizedFeature, cancellationToken);
        bool? previousValue = existing?.IsEnabled;
        if (existing is not null)
        {
            featureOverrideRepository.Remove(existing);
            await featureOverrideRepository.SaveChangesAsync(cancellationToken);
        }

        await auditService.LogAsync(
            executorUserId,
            "CLEAR_FEATURE_OVERRIDE",
            "UserFeatureOverride",
            id.ToString(),
            ipAddress,
            userAgent,
            BuildClearOverrideMetadata(id, normalizedFeature, previousValue),
            cancellationToken);

        var overrides = await featureOverrideRepository.ListByUserAsync(id, cancellationToken);
        return BuildFeatureAccessResponse(user.Role, overrides);
    }

    public async Task DeleteAsync(Guid id, Guid currentUserId, CancellationToken cancellationToken)
    {
        if (id == currentUserId)
        {
            throw new AppProblemException("Ação inválida", "Você não pode excluir o próprio usuário.", StatusCodes.Status400BadRequest);
        }

        var user = await userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppProblemException("Usuário não encontrado", "Usuário não encontrado.", StatusCodes.Status404NotFound);

        userRepository.Remove(user);
        try
        {
            await userRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new AppProblemException(
                "Falha ao salvar",
                "Não foi possível excluir o usuário no momento.",
                StatusCodes.Status409Conflict);
        }
    }

    public async Task<SubscriptionChangeResponse> GrantTrialAsync(Guid id, GrantTrialRequest request, CancellationToken cancellationToken)
    {
        if (request.Days <= 0 || request.Days > 365)
            throw new AppProblemException("Parâmetro inválido", "O número de dias deve ser entre 1 e 365.", StatusCodes.Status400BadRequest);

        var plan = SubscriptionPlanCatalog.GetByCodeOrThrow(request.PlanCode);
        if (plan.Role == UserRole.Admin || plan.Code == "basic")
            throw new AppProblemException("Plano inválido", "O trial deve ser de um plano pago (intermediate ou advanced).", StatusCodes.Status400BadRequest);

        var user = await userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppProblemException("Usuário não encontrado", "Usuário não encontrado.", StatusCodes.Status404NotFound);

        var now = DateTime.UtcNow;
        var subscription = await userSubscriptionRepository.GetByUserIdAsync(id, cancellationToken);

        if (subscription is null)
        {
            subscription = new UserSubscription(id, plan.Code, plan.Role, SubscriptionBillingCycle.Monthly, 0m, "BRL", now, now.AddDays(request.Days));
            await userSubscriptionRepository.AddAsync(subscription, cancellationToken);
        }

        subscription.ActivateTrial(plan.Code, plan.Role, now, now.AddDays(request.Days));
        user.SetRole(plan.Role);
        await userRepository.SaveChangesAsync(cancellationToken);
        await userSubscriptionRepository.SaveChangesAsync(cancellationToken);

        var session = await userSessionService.ReissueAsync(user, now, cancellationToken);
        return new SubscriptionChangeResponse(SubscriptionResponseFactory.BuildCurrent(user, subscription), session, SubscriptionResponseFactory.Notes);
    }

    private static IReadOnlyList<UserFeatureAccessResponse> BuildFeatureAccessResponse(UserRole role, IReadOnlyList<UserFeatureOverride> overrides)
    {
        var baseFeatures = new HashSet<string>(
            FeatureAccessEvaluator.GetRoleFeatures(role),
            StringComparer.OrdinalIgnoreCase);
        var overrideMap = overrides.ToDictionary(x => x.FeatureKey, x => x.IsEnabled, StringComparer.OrdinalIgnoreCase);

        return AppFeatureKeys.All
            .OrderBy(x => x)
            .Select(feature =>
            {
                var enabledByRole = baseFeatures.Contains(feature);
                var hasOverride = overrideMap.TryGetValue(feature, out var overrideEnabled);
                var effectiveEnabled = hasOverride ? overrideEnabled : enabledByRole;
                return new UserFeatureAccessResponse(feature, effectiveEnabled, enabledByRole, hasOverride ? overrideEnabled : null);
            })
            .ToList();
    }

    private static string NormalizeFeatureOrThrow(string featureKey)
    {
        var normalized = featureKey.Trim();
        if (!AppFeatureKeys.IsKnownFeature(normalized))
        {
            throw new AppProblemException("Funcionalidade inválida", "A funcionalidade informada não é reconhecida.", StatusCodes.Status400BadRequest);
        }

        return normalized;
    }

    private static string BuildSetOverrideMetadata(Guid targetUserId, string featureKey, bool? previousValue, bool newValue)
    {
        return JsonSerializer.Serialize(new
        {
            targetUserId,
            featureKey,
            previousValue,
            newValue
        });
    }

    private static string BuildClearOverrideMetadata(Guid targetUserId, string featureKey, bool? previousValue)
    {
        return JsonSerializer.Serialize(new
        {
            targetUserId,
            featureKey,
            previousValue,
            cleared = true
        });
    }
}
