using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;

namespace InvestindoEmNegocio.Application.Services;

public sealed class BillingNotificationService(
    IUserRepository userRepository,
    IBillingCheckoutRepository billingCheckoutRepository,
    IUserNotificationRepository notificationRepository,
    IEmailSender emailSender,
    ILogger<BillingNotificationService> logger) : IBillingNotificationService
{
    public async Task NotifyPendingAsync(User user, BillingCheckout checkout, CancellationToken cancellationToken = default)
    {
        if (checkout.EmailPendingSent)
            return;

        await AddInAppNotificationAsync(user.Id, NotificationKind.BillingPending, checkout, "Cobrança iniciada", $"A cobrança do plano {checkout.PlanCode} foi iniciada e aguarda confirmação.", cancellationToken);
        await TrySendEmailAsync(user.Email, "Sua cobrança foi iniciada", BuildEmailHtml("Cobranca iniciada", $"A contratação do plano {checkout.PlanCode} foi iniciada e aguarda confirmação de pagamento."), cancellationToken);
        checkout.MarkPendingEmailSent(DateTime.UtcNow);
        await billingCheckoutRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task NotifyApprovedAsync(Guid userId, BillingCheckout checkout, CancellationToken cancellationToken = default)
    {
        if (checkout.EmailSuccessSent)
            return;

        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        await AddInAppNotificationAsync(userId, NotificationKind.BillingApproved, checkout, "Plano ativado", $"Seu pagamento foi confirmado e o plano {checkout.PlanCode} está ativo.", cancellationToken);
        await TrySendEmailAsync(user.Email, "Pagamento aprovado", BuildEmailHtml("Pagamento aprovado", $"Seu pagamento foi confirmado e o plano {checkout.PlanCode} agora está ativo."), cancellationToken);
        checkout.MarkSuccessEmailSent(DateTime.UtcNow);
        await billingCheckoutRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task NotifyFailedAsync(Guid userId, BillingCheckout checkout, CancellationToken cancellationToken = default, string? customMessage = null)
    {
        if (checkout.EmailFailureSent)
            return;

        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        var message = customMessage ?? checkout.FailureReason ?? "A cobrança não foi aprovada e precisa de nova ação.";
        await AddInAppNotificationAsync(userId, NotificationKind.BillingFailed, checkout, "Falha na cobrança", message, cancellationToken);
        await TrySendEmailAsync(user.Email, "Falha na cobrança", BuildEmailHtml("Falha na cobranca", message), cancellationToken);
        checkout.MarkFailureEmailSent(DateTime.UtcNow);
        await billingCheckoutRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task<User> GetUserOrThrowAsync(Guid userId, CancellationToken cancellationToken)
        => await userRepository.GetByIdAsync(userId, cancellationToken)
           ?? throw new Application.Exceptions.AppProblemException("Usuário não encontrado", "Usuário não encontrado.", StatusCodes.Status404NotFound);

    private async Task AddInAppNotificationAsync(Guid userId, NotificationKind kind, BillingCheckout checkout, string title, string message, CancellationToken cancellationToken)
    {
        var referenceKey = $"billing:{checkout.Id}:{kind}";
        if (await notificationRepository.ExistsAsync(userId, referenceKey, cancellationToken))
            return;

        await notificationRepository.AddRangeAsync(
            [new UserNotification(userId, kind, title, message, referenceKey, payloadJson: $$"""{"checkoutId":"{{checkout.Id}}","planCode":"{{checkout.PlanCode}}","status":"{{checkout.Status}}"}""")],
            cancellationToken);
        await notificationRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task TrySendEmailAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        try
        {
            await emailSender.SendAsync(to, subject, htmlBody, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send billing email {Subject} to {Recipient}", subject, to);
        }
    }

    private static string BuildEmailHtml(string title, string body)
    {
        return $"""
                <html lang="pt-BR">
                <body style="font-family: Arial, sans-serif; color: #0f172a;">
                  <h2>{title}</h2>
                  <p>{body}</p>
                </body>
                </html>
                """;
    }
}
