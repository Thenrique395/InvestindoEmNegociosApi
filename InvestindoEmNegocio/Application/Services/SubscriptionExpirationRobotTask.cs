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
}
