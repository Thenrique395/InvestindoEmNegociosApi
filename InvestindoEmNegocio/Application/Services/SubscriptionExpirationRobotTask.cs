using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

public sealed class SubscriptionExpirationRobotTask(
    IUserRepository userRepository,
    IUserSubscriptionRepository userSubscriptionRepository,
    IBillingNotificationService billingNotificationService,
    IPaymentProviderResolver paymentProviderResolver,
    IBillingSubscriptionSyncService billingSubscriptionSyncService,
    ILogger<SubscriptionExpirationRobotTask> logger) : IRobotTask
{
    public string Name => "RoboExpiracaoAssinaturas";

    private const int GracePeriodDays = 7;

    public async Task<RobotTaskExecutionResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var dueSubscriptions = await userSubscriptionRepository.ListDueForExpirationAsync(now, cancellationToken);

        var graceCutoff = now.AddDays(-GracePeriodDays);
        var pastDueExpired = await userSubscriptionRepository.ListPastDueExpiredGraceAsync(graceCutoff, cancellationToken);

        // Subscriptions in last 24h of grace period (RenewsAt between now-7d and now-6d) → reminder
        var reminderFrom = now.AddDays(-GracePeriodDays);
        var reminderTo = now.AddDays(-(GracePeriodDays - 1));
        var approachingExpiry = await userSubscriptionRepository.ListPastDueApproachingGraceEndAsync(reminderFrom, reminderTo, cancellationToken);

        logger.LogInformation("{Name} starting: {DueCount} due for cancellation, {PastDueCount} past grace period, {ReminderCount} approaching grace end",
            Name, dueSubscriptions.Count, pastDueExpired.Count, approachingExpiry.Count);

        if (dueSubscriptions.Count == 0 && pastDueExpired.Count == 0 && approachingExpiry.Count == 0)
            return new RobotTaskExecutionResult(0, ZeroItemsReasonCode: "NO_SUBSCRIPTIONS_DUE");

        var processed = 0;

        foreach (var subscription in approachingExpiry)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var user = await userRepository.GetByIdAsync(subscription.UserId, cancellationToken);
            if (user is null || user.Role == UserRole.Admin)
                continue;

            var graceEndsAt = subscription.RenewsAt.AddDays(GracePeriodDays);
            logger.LogInformation("{Name}: sending grace period reminder to user {UserId} (plan {PlanCode}), grace ends {GraceEndsAt:u}",
                Name, subscription.UserId, subscription.PlanCode, graceEndsAt);
            await billingNotificationService.NotifyGracePeriodReminderAsync(subscription.UserId, subscription.PlanCode, graceEndsAt, cancellationToken);
        }

        foreach (var subscription in dueSubscriptions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var user = await userRepository.GetByIdAsync(subscription.UserId, cancellationToken);
            if (user is null || user.Role == UserRole.Admin)
                continue;

            // Verifica Stripe antes de cancelar — webhook de "renovação retomada" pode ter sido perdido
            if (await TryReconcileWithProviderAsync(subscription, cancellationToken))
            {
                processed++;
                continue;
            }

            logger.LogInformation("{Name}: cancelling subscription for user {UserId} (plan {PlanCode}) — AutoRenew disabled and RenewsAt passed",
                Name, subscription.UserId, subscription.PlanCode);
            subscription.CancelNow(now);
            user.SetRole(UserRole.Basic);
            processed++;
        }

        foreach (var subscription in pastDueExpired)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var user = await userRepository.GetByIdAsync(subscription.UserId, cancellationToken);
            if (user is null || user.Role == UserRole.Admin)
                continue;

            // Verifica Stripe antes de downgrade — webhook pode ter sido perdido
            if (await TryReconcileWithProviderAsync(subscription, cancellationToken))
            {
                processed++;
                continue;
            }

            logger.LogWarning("{Name}: expiring PastDue subscription for user {UserId} (plan {PlanCode}) — grace period ended, downgrading to Basic",
                Name, subscription.UserId, subscription.PlanCode);
            subscription.MarkExpired(now);
            user.SetRole(UserRole.Basic);
            await billingNotificationService.NotifyDowngradedAsync(subscription.UserId, subscription.PlanCode, cancellationToken);
            processed++;
        }

        await userRepository.SaveChangesAsync(cancellationToken);
        await userSubscriptionRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("{Name} completed: {Processed} subscriptions processed", Name, processed);
        return new RobotTaskExecutionResult(processed);
    }

    /// <summary>
    /// Verifica o status real no gateway de pagamento antes de qualquer efeito local
    /// irreversível (cancelamento/downgrade). Retorna true se sincronizou a partir do
    /// gateway (e o caller deve pular a ação local).
    /// </summary>
    private async Task<bool> TryReconcileWithProviderAsync(Domain.Entities.UserSubscription subscription, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subscription.ExternalSubscriptionId))
            return false;

        try
        {
            var provider = paymentProviderResolver.Resolve(subscription.Provider);
            var snapshot = await provider.GetSubscriptionAsync(subscription.ExternalSubscriptionId, cancellationToken);
            var providerStatus = snapshot.Status?.ToLowerInvariant() ?? string.Empty;
            if (providerStatus is not ("active" or "trialing"))
                return false;

            logger.LogInformation("{Name}: provider shows subscription {ExternalId} as {ProviderStatus} for user {UserId} — syncing instead of applying local action",
                Name, subscription.ExternalSubscriptionId, providerStatus, subscription.UserId);
            await billingSubscriptionSyncService.SyncAsync(snapshot, "robot.reconcile", cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Name}: failed to verify provider status for subscription {ExternalId} (user {UserId}) — proceeding with local decision",
                Name, subscription.ExternalSubscriptionId, subscription.UserId);
            return false;
        }
    }
}
