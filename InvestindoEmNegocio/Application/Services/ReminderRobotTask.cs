using System.Net;
using System.Text;
using System.Linq;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

public sealed class ReminderRobotTask(
    IUserRepository userRepository,
    IUserProfileRepository userProfileRepository,
    IUserNotificationRepository userNotificationRepository,
    INotificationsService notificationsService,
    IEmailSender emailSender,
    ILogger<ReminderRobotTask> logger) : IRobotTask
{
    public string Name => "RoboLembretes";

    public async Task<RobotTaskExecutionResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var users = await userRepository.ListAsync(cancellationToken);
        if (users.Count == 0)
            return new RobotTaskExecutionResult(0, ZeroItemsReasonCode: "NO_USERS");

        var totalGenerated = 0;
        var emailsAttempted = 0;
        var emailsSent = 0;
        var emailsFailed = 0;
        var todayUtc = DateTime.UtcNow.Date;
        var activeUsersCount = users.Count(u => u.IsActive);
        if (activeUsersCount == 0)
            return new RobotTaskExecutionResult(0, ZeroItemsReasonCode: "NO_ACTIVE_USERS");

        foreach (var user in users.Where(u => u.IsActive))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var generated = await notificationsService.GenerateAsync(user.Id, cancellationToken);
            totalGenerated += generated;

            if (generated <= 0) continue;

            var profile = await userProfileRepository.GetByUserIdAsync(user.Id, cancellationToken);
            if (profile is null || !profile.NotifyEmailEnabled) continue;

            var unread = await userNotificationRepository.ListByUserAsync(
                user.Id,
                unreadOnly: true,
                limit: 50,
                cancellationToken);

            var toEmail = unread
                .Where(n => n.CreatedAt >= todayUtc)
                .OrderByDescending(n => n.CreatedAt)
                .Take(20)
                .ToList();

            if (toEmail.Count == 0) continue;

            var subject = $"Você tem {toEmail.Count} lembrete(s) financeiro(s)";
            var htmlBody = BuildHtmlBody(user.Name, toEmail);
            var textBody = BuildTextBody(toEmail);

            try
            {
                emailsAttempted++;
                await emailSender.SendAsync(user.Email, subject, htmlBody, textBody, cancellationToken);
                emailsSent++;
                logger.LogInformation(
                    "RoboLembretes enviou resumo por e-mail para {Email} com {Count} lembrete(s).",
                    user.Email,
                    toEmail.Count);
            }
            catch (Exception ex)
            {
                emailsFailed++;
                logger.LogError(ex, "RoboLembretes falhou ao enviar e-mail para {Email}.", user.Email);
            }
        }

        var zeroItemsReasonCode = totalGenerated == 0 ? "NO_NEW_NOTIFICATIONS" : null;
        return new RobotTaskExecutionResult(
            totalGenerated,
            emailsAttempted,
            emailsSent,
            emailsFailed,
            zeroItemsReasonCode);
    }

    private static string BuildHtmlBody(string userName, IReadOnlyList<Domain.Entities.UserNotification> notifications)
    {
        var safeName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(userName) ? "usuário" : userName.Trim());
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Arial,sans-serif;line-height:1.5;color:#0f172a\">");
        sb.Append("<h2 style=\"margin:0 0 12px\">Lembretes financeiros do dia</h2>");
        sb.Append($"<p>Olá, {safeName}. Estes são seus lembretes gerados hoje:</p>");
        sb.Append("<ul style=\"padding-left:18px\">");
        foreach (var item in notifications)
        {
            var title = WebUtility.HtmlEncode(item.Title);
            var message = WebUtility.HtmlEncode(item.Message);
            sb.Append($"<li><strong>{title}</strong><br/>{message}</li>");
        }
        sb.Append("</ul>");
        sb.Append("<p style=\"margin-top:16px;color:#64748b\">Abra o app para ver os detalhes e marcar como concluído.</p>");
        sb.Append("</div>");
        return sb.ToString();
    }

    private static string BuildTextBody(IReadOnlyList<Domain.Entities.UserNotification> notifications)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Lembretes financeiros do dia:");
        foreach (var item in notifications)
        {
            sb.AppendLine($"- {item.Title}: {item.Message}");
        }
        sb.AppendLine();
        sb.AppendLine("Abra o app para ver os detalhes.");
        return sb.ToString();
    }
}
