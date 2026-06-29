using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using InvestindoEmNegocio.Domain.Security;

namespace InvestindoEmNegocio.Application.Services;

/// <summary>
/// Roda diariamente (mesmo ciclo do <see cref="ReminderRobotTask"/>) e avisa proativamente, via
/// notificação, quando o agente de saúde financeira (<see cref="IAiFinancialHealthService"/>)
/// encontra alguma área não-"ok" para um usuário — sem o usuário precisar abrir o app ou pedir.
/// </summary>
public sealed class AiFinancialHealthRobotTask(
    IUserRepository userRepository,
    IUserProfileRepository userProfileRepository,
    IUserNotificationRepository userNotificationRepository,
    IAiFinancialHealthService aiFinancialHealthService) : IRobotTask
{
    public string Name => "RoboSaudeFinanceiraIA";

    public async Task<RobotTaskExecutionResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var users = await userRepository.ListAsync(cancellationToken);
        var eligibleUsers = users
            .Where(u => u.IsActive && AppFeatureMatrix.HasRoleFeature(u.Role, AppFeatureKeys.FinancialAssistantAccess))
            .ToList();

        if (eligibleUsers.Count == 0)
            return new RobotTaskExecutionResult(0, ZeroItemsReasonCode: "NO_ELIGIBLE_USERS");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var created = 0;

        foreach (var user in eligibleUsers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var health = await aiFinancialHealthService.GetHealthAsync(user.Id, today, cancellationToken);
            if (health.OverallStatus == "ok")
                continue;

            var profile = await userProfileRepository.GetByUserIdAsync(user.Id, cancellationToken);
            if (profile is null || !profile.NotifyInAppEnabled)
                continue;

            var referenceKey = $"ai-health:{user.Id}:{today:yyyyMMdd}";
            if (await userNotificationRepository.ExistsAsync(user.Id, referenceKey, cancellationToken))
                continue;

            var title = health.OverallStatus == "critical" ? "Atenção: situação financeira crítica" : "Atenção financeira recomendada";
            var concerningAreas = health.Areas.Where(a => a.Status != "ok").Select(a => a.Explanation);
            var message = string.IsNullOrWhiteSpace(health.OverallSummary)
                ? string.Join(" ", concerningAreas)
                : health.OverallSummary;

            var notification = new UserNotification(user.Id, NotificationKind.AiHealthAlert, title, message, referenceKey, dueDate: today);
            await userNotificationRepository.AddRangeAsync([notification], cancellationToken);
            await userNotificationRepository.SaveChangesAsync(cancellationToken);
            created++;
        }

        var zeroItemsReasonCode = created == 0 ? "NO_NEW_ALERTS" : null;
        return new RobotTaskExecutionResult(created, ZeroItemsReasonCode: zeroItemsReasonCode);
    }
}
