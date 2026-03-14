using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Application.Services;

public class PreferencesService(
    IUserProfileRepository profileRepository,
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IAuditService auditService,
    IInvestDbContext dbContext) : IPreferencesService
{
    private static NotificationPreferencesDto BuildDefaultNotifications() =>
        new(true, true, true, false, 3);

    public async Task<PreferencesDto> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await profileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
            return new PreferencesDto("BRL", new List<string> { "pt-BR" }, BuildDefaultNotifications());


        var locales = (profile.Language ?? "pt-BR")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (locales.Count == 0) locales.Add("pt-BR");
        var notifications = new NotificationPreferencesDto(
            profile.NotifyUpcomingEnabled,
            profile.NotifyOverdueEnabled,
            profile.NotifyInAppEnabled,
            profile.NotifyEmailEnabled,
            profile.NotifyDaysBeforeDue);
        return new PreferencesDto(profile.Currency ?? "BRL", locales, notifications);
    }

    public async Task<PreferencesDto> UpdateAsync(Guid userId, UpdatePreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        var profile = await profileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            var primaryLocale = request.Locales.FirstOrDefault() ?? "pt-BR";
            profile = new UserProfile(userId, string.Empty, string.Empty, string.Empty, null, string.Empty,
                string.Empty, string.Empty, string.Empty, primaryLocale, request.Currency);
            await profileRepository.AddAsync(profile, cancellationToken);
        }

        profile.GetType().GetProperty("Language")?.SetValue(profile, string.Join(';', request.Locales));
        profile.GetType().GetProperty("Currency")?.SetValue(profile, request.Currency);
        if (request.Notifications is not null)
        {
            profile.SetNotificationPreferences(
                request.Notifications.UpcomingEnabled,
                request.Notifications.OverdueEnabled,
                request.Notifications.EmailEnabled,
                request.Notifications.InAppEnabled,
                request.Notifications.DaysBeforeDue);
        }
        profile.GetType().GetProperty("UpdatedAt")?.SetValue(profile, DateTime.UtcNow);

        await profileRepository.SaveChangesAsync(cancellationToken);
        var notifications = request.Notifications ?? BuildDefaultNotifications();
        return new PreferencesDto(request.Currency, request.Locales, notifications);
    }

    public async Task<PrivacySummaryDto> GetPrivacySummaryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var activeSessions = await dbContext.RefreshTokens
            .CountAsync(x => x.UserId == userId && !x.RevokedAt.HasValue && x.ExpiresAt > now, cancellationToken);
        var pendingPasswordResetRequests = await dbContext.PasswordResetTokens
            .CountAsync(x => x.UserId == userId && !x.UsedAt.HasValue && x.ExpiresAt > now, cancellationToken);
        var auditEntries = await dbContext.AuditLogs.CountAsync(x => x.UserId == userId, cancellationToken);

        return new PrivacySummaryDto(
            activeSessions,
            pendingPasswordResetRequests,
            auditEntries,
            DataExportEnabled: true,
            SelfServiceDeletionEnabled: true,
            DeletionScope:
            [
                "perfil e preferências",
                "contas e transações",
                "cartões, parcelas e pagamentos",
                "metas, categorias e notificações",
                "investimentos, movimentos e onboarding",
                "tokens de sessão, reset de senha e trilha de auditoria"
            ],
            ProductionControls:
            [
                "JWT com refresh token",
                "revogação self-service de sessões",
                "lockout por tentativas inválidas",
                "segregação por UserId",
                "rate limiting",
                "observabilidade OpenTelemetry",
                "compressão de resposta",
                "resiliência HTTP com retry"
            ],
            ScalabilityPhase: "phase-1-runtime-hardened",
            RetentionPolicy: "A exclusão self-service remove os dados operacionais da conta e revoga artefatos de autenticação imediatamente.");
    }

    public async Task<SecuritySummaryDto> GetSecuritySummaryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new AppProblemException("Usuário não encontrado", "Usuário não encontrado.", StatusCodes.Status404NotFound);

        var activeSessions = await dbContext.RefreshTokens
            .CountAsync(x => x.UserId == userId && !x.RevokedAt.HasValue && x.ExpiresAt > now, cancellationToken);

        return new SecuritySummaryDto(
            activeSessions,
            user.FailedLoginAttempts,
            user.IsLocked(now),
            user.LockoutUntil,
            user.LastLoginAt,
            Controls:
            [
                "JWT com refresh token rotativo",
                "lockout após múltiplas tentativas inválidas",
                "revogação de sessões ativas pelo próprio usuário",
                "revalidação de senha para exclusão da conta"
            ],
            Recommendations:
            [
                "Troque a senha após uso em dispositivo compartilhado.",
                "Revogue sessões ao suspeitar de acesso indevido.",
                "Mantenha e-mail e senha de recuperação atualizados."
            ]);
    }

    public async Task<RevokeSessionsResponse> RevokeOwnSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var activeSessions = await dbContext.RefreshTokens
            .Where(x => x.UserId == userId && !x.RevokedAt.HasValue && x.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var token in activeSessions)
        {
            token.Revoke(now);
        }

        await refreshTokenRepository.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(userId, "REVOKE_OWN_SESSIONS", "RefreshToken", userId.ToString(), null, null, $"count={activeSessions.Count}", cancellationToken);

        return new RevokeSessionsResponse(activeSessions.Count, now);
    }

    public async Task DeleteOwnAccountAsync(Guid userId, DeleteOwnAccountRequest request, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.ConfirmationText?.Trim(), "EXCLUIR", StringComparison.OrdinalIgnoreCase))
        {
            throw new AppProblemException(
                "Confirmação inválida",
                "Para excluir a conta, digite EXCLUIR no campo de confirmação.",
                StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            throw new AppProblemException(
                "Senha obrigatória",
                "Informe sua senha atual para confirmar a exclusão da conta.",
                StatusCodes.Status400BadRequest);
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new AppProblemException("Usuário não encontrado", "Usuário não encontrado.", StatusCodes.Status404NotFound);

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new AppProblemException(
                "Senha inválida",
                "A senha informada está incorreta.",
                StatusCodes.Status400BadRequest);
        }

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        await RemoveUserDataAsync(userId, cancellationToken);
        userRepository.Remove(user);
        await userRepository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task RemoveUserDataAsync(Guid userId, CancellationToken cancellationToken)
    {
        var positionIds = await dbContext.InvestmentPositions
            .Where(x => x.UserId == userId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        dbContext.RefreshTokens.RemoveRange(dbContext.RefreshTokens.Where(x => x.UserId == userId));
        dbContext.PasswordResetTokens.RemoveRange(dbContext.PasswordResetTokens.Where(x => x.UserId == userId));
        dbContext.AuditLogs.RemoveRange(dbContext.AuditLogs.Where(x => x.UserId == userId));
        dbContext.MoneyPayments.RemoveRange(dbContext.MoneyPayments.Where(x => x.UserId == userId));
        dbContext.MoneyInstallments.RemoveRange(dbContext.MoneyInstallments.Where(x => x.UserId == userId));
        dbContext.GoalContributions.RemoveRange(dbContext.GoalContributions.Where(x => x.UserId == userId));
        dbContext.UserCategorizationFeedback.RemoveRange(dbContext.UserCategorizationFeedback.Where(x => x.UserId == userId));
        dbContext.RobotExecutionLogs.RemoveRange(dbContext.RobotExecutionLogs.Where(x => x.TriggeredByUserId == userId));
        dbContext.UserSubscriptions.RemoveRange(dbContext.UserSubscriptions.Where(x => x.UserId == userId));
        dbContext.InvestmentMovements.RemoveRange(dbContext.InvestmentMovements.Where(x => positionIds.Contains(x.PositionId)));
        dbContext.UserNotifications.RemoveRange(dbContext.UserNotifications.Where(x => x.UserId == userId));
        dbContext.InvestmentPositions.RemoveRange(dbContext.InvestmentPositions.Where(x => x.UserId == userId));
        dbContext.InvestmentGoals.RemoveRange(dbContext.InvestmentGoals.Where(x => x.UserId == userId));
        dbContext.InvestmentAllocationTargets.RemoveRange(dbContext.InvestmentAllocationTargets.Where(x => x.UserId == userId));
        dbContext.AccountTransactions.RemoveRange(dbContext.AccountTransactions.Where(x => x.UserId == userId));
        dbContext.MoneyPlans.RemoveRange(dbContext.MoneyPlans.Where(x => x.UserId == userId));
        dbContext.Goals.RemoveRange(dbContext.Goals.Where(x => x.UserId == userId));
        dbContext.Cards.RemoveRange(dbContext.Cards.Where(x => x.UserId == userId));
        dbContext.Categories.RemoveRange(dbContext.Categories.Where(x => x.UserId == userId));
        dbContext.Accounts.RemoveRange(dbContext.Accounts.Where(x => x.UserId == userId));
        dbContext.UserOnboardings.RemoveRange(dbContext.UserOnboardings.Where(x => x.UserId == userId));
        dbContext.UserProfiles.RemoveRange(dbContext.UserProfiles.Where(x => x.UserId == userId));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
