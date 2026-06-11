using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public sealed class SubscriptionExpirationRobotTask(
    IUserRepository userRepository,
    IUserSubscriptionRepository userSubscriptionRepository) : IRobotTask
{
    public string Name => "RoboExpiracaoAssinaturas";

    public async Task<RobotTaskExecutionResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var dueSubscriptions = await userSubscriptionRepository.ListDueForExpirationAsync(now, cancellationToken);
        if (dueSubscriptions.Count == 0)
            return new RobotTaskExecutionResult(0, ZeroItemsReasonCode: "NO_SUBSCRIPTIONS_DUE");

        var processed = 0;
        foreach (var subscription in dueSubscriptions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var user = await userRepository.GetByIdAsync(subscription.UserId, cancellationToken);
            if (user is null || user.Role == UserRole.Admin)
                continue;

            subscription.CancelNow(now);
            user.SetRole(UserRole.Basic);
            processed++;
        }

        await userRepository.SaveChangesAsync(cancellationToken);
        await userSubscriptionRepository.SaveChangesAsync(cancellationToken);

        return new RobotTaskExecutionResult(processed);
    }
}
