using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Application.Services;

public sealed class UserPrivacyCenterService(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IAuditService auditService,
    IInvestDbContext dbContext) : IUserPrivacyCenterService
{
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
            token.Revoke(now);

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

        var now = DateTime.UtcNow;
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        await RemoveUserDataAsync(userId, cancellationToken);
        user.Anonymize(now);
        await userRepository.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(userId, "ACCOUNT_ANONYMIZED", "User", userId.ToString(), null, null, $"deletedAt={now:u}", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task RemoveUserDataAsync(Guid userId, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters: a exclusão de conta precisa apagar TUDO de verdade, inclusive
        // o que o próprio usuário já havia soft-deletado antes (conta/cartão/meta/transação/
        // plano/posição) — senão essas linhas nunca mais seriam alcançadas por nenhuma query
        // e a exclusão de conta mentiria sobre o que de fato remove.
        var positionIds = await dbContext.InvestmentPositions
            .IgnoreQueryFilters()
            .Where(x => x.UserId == userId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        // Artefatos de autenticação — removidos imediatamente
        dbContext.RefreshTokens.RemoveRange(dbContext.RefreshTokens.Where(x => x.UserId == userId));
        dbContext.PasswordResetTokens.RemoveRange(dbContext.PasswordResetTokens.Where(x => x.UserId == userId));

        // Dados operacionais — removidos (direito ao esquecimento LGPD)
        dbContext.MoneyPayments.RemoveRange(dbContext.MoneyPayments.IgnoreQueryFilters().Where(x => x.UserId == userId));
        dbContext.MoneyInstallments.RemoveRange(dbContext.MoneyInstallments.IgnoreQueryFilters().Where(x => x.UserId == userId));
        dbContext.GoalContributions.RemoveRange(dbContext.GoalContributions.IgnoreQueryFilters().Where(x => x.UserId == userId));
        dbContext.UserCategorizationFeedback.RemoveRange(dbContext.UserCategorizationFeedback.Where(x => x.UserId == userId));
        dbContext.RobotExecutionLogs.RemoveRange(dbContext.RobotExecutionLogs.Where(x => x.TriggeredByUserId == userId));
        dbContext.InvestmentMovements.RemoveRange(dbContext.InvestmentMovements.IgnoreQueryFilters().Where(x => positionIds.Contains(x.PositionId)));
        dbContext.UserNotifications.RemoveRange(dbContext.UserNotifications.Where(x => x.UserId == userId));
        dbContext.InvestmentPositions.RemoveRange(dbContext.InvestmentPositions.IgnoreQueryFilters().Where(x => x.UserId == userId));
        dbContext.InvestmentGoals.RemoveRange(dbContext.InvestmentGoals.Where(x => x.UserId == userId));
        dbContext.InvestmentAllocationTargets.RemoveRange(dbContext.InvestmentAllocationTargets.Where(x => x.UserId == userId));
        dbContext.AccountTransactions.RemoveRange(dbContext.AccountTransactions.IgnoreQueryFilters().Where(x => x.UserId == userId));
        dbContext.MoneyPlans.RemoveRange(dbContext.MoneyPlans.IgnoreQueryFilters().Where(x => x.UserId == userId));
        dbContext.Goals.RemoveRange(dbContext.Goals.IgnoreQueryFilters().Where(x => x.UserId == userId));
        dbContext.Cards.RemoveRange(dbContext.Cards.IgnoreQueryFilters().Where(x => x.UserId == userId));
        dbContext.Categories.RemoveRange(dbContext.Categories.Where(x => x.UserId == userId));
        dbContext.Accounts.RemoveRange(dbContext.Accounts.IgnoreQueryFilters().Where(x => x.UserId == userId));
        dbContext.UserOnboardings.RemoveRange(dbContext.UserOnboardings.Where(x => x.UserId == userId));
        dbContext.UserProfiles.RemoveRange(dbContext.UserProfiles.Where(x => x.UserId == userId));

        // UserSubscriptions e BillingCheckouts são retidos anonimizados — necessário para
        // conformidade fiscal/tributária (retenção mínima de 5 anos).
        // AuditLogs são retidos como trilha de auditoria de segurança.
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
