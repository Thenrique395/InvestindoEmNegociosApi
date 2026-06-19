using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Application.Services;

public sealed class BillingNotificationService(
    IUserRepository userRepository,
    IBillingCheckoutRepository billingCheckoutRepository,
    IUserNotificationRepository notificationRepository,
    IEmailSender emailSender,
    IOptions<StripeOptions> stripeOptions,
    ILogger<BillingNotificationService> logger) : IBillingNotificationService
{
    private string FrontendBase => stripeOptions.Value.FrontendBaseUrl.TrimEnd('/');

    public async Task NotifyPendingAsync(User user, BillingCheckout checkout, CancellationToken cancellationToken = default)
    {
        if (checkout.EmailPendingSent)
            return;

        var planName = PlanDisplayName(checkout.PlanCode);
        await AddInAppNotificationAsync(user.Id, NotificationKind.BillingPending, checkout,
            "Cobrança em processamento",
            $"A contratação do plano {planName} está sendo processada. Você receberá uma confirmação assim que o pagamento for aprovado.",
            cancellationToken);
        await TrySendEmailAsync(user.Email,
            "Sua cobrança está sendo processada",
            BuildEmailHtml(
                "Cobrança em processamento",
                $"Sua contratação do plano {planName} está sendo processada. Aguarde a confirmação — você receberá um novo e-mail assim que o pagamento for aprovado."),
            cancellationToken);
        checkout.MarkPendingEmailSent(DateTime.UtcNow);
        await billingCheckoutRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task NotifyApprovedAsync(Guid userId, BillingCheckout checkout, CancellationToken cancellationToken = default)
    {
        if (checkout.EmailSuccessSent)
            return;

        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        var planName = PlanDisplayName(checkout.PlanCode);
        await AddInAppNotificationAsync(userId, NotificationKind.BillingApproved, checkout,
            "Plano ativado",
            $"Pagamento confirmado! O plano {planName} está ativo na sua conta.",
            cancellationToken);
        await TrySendEmailAsync(user.Email,
            $"Bem-vindo ao plano {planName}!",
            BuildEmailHtml(
                $"Plano {planName} ativado",
                $"Ótimo! Seu pagamento foi aprovado e o plano {planName} já está disponível na sua conta. Acesse o app e aproveite todos os recursos.",
                $"{FrontendBase}/dashboard",
                "Acessar o app"),
            cancellationToken);
        checkout.MarkSuccessEmailSent(DateTime.UtcNow);
        await billingCheckoutRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task NotifyFailedAsync(Guid userId, BillingCheckout checkout, CancellationToken cancellationToken = default, string? customMessage = null)
    {
        if (checkout.EmailFailureSent)
            return;

        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        var message = customMessage ?? checkout.FailureReason ?? "A cobrança não foi aprovada. Atualize sua forma de pagamento para continuar com acesso ao plano.";
        var isRenewalFailure = checkout.Status == BillingCheckoutStatus.Paid;
        var emailSubject = isRenewalFailure
            ? "Problema ao renovar sua assinatura"
            : "Problema na confirmação do pagamento";

        await AddInAppNotificationAsync(userId, NotificationKind.BillingFailed, checkout, "Falha na cobrança", message, cancellationToken);
        await TrySendEmailAsync(user.Email,
            emailSubject,
            BuildEmailHtml("Falha na cobrança", message, $"{FrontendBase}/assinatura", "Atualizar pagamento"),
            cancellationToken);
        checkout.MarkFailureEmailSent(DateTime.UtcNow);
        await billingCheckoutRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task NotifyRenewalApprovedAsync(Guid userId, BillingCheckout checkout, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        var referenceKey = $"billing:{checkout.Id}:{NotificationKind.BillingRenewalApproved}:{DateTime.UtcNow:yyyyMM}";
        if (await notificationRepository.ExistsAsync(userId, referenceKey, cancellationToken))
            return;

        var planName = PlanDisplayName(checkout.PlanCode);
        var msg = $"Tudo certo! Sua assinatura do plano {planName} foi renovada automaticamente. Nenhuma ação necessária.";
        await notificationRepository.AddRangeAsync(
            [new UserNotification(userId, NotificationKind.BillingRenewalApproved, "Assinatura renovada", msg, referenceKey,
                payloadJson: $$"""{"checkoutId":"{{checkout.Id}}","planCode":"{{checkout.PlanCode}}"}""")],
            cancellationToken);
        await notificationRepository.SaveChangesAsync(cancellationToken);
        await TrySendEmailAsync(user.Email,
            $"Assinatura do plano {planName} renovada",
            BuildEmailHtml("Assinatura renovada", msg),
            cancellationToken);
    }

    public async Task NotifyReactivatedAsync(Guid userId, BillingCheckout checkout, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        var referenceKey = $"billing:{checkout.Id}:{NotificationKind.BillingReactivated}";
        if (await notificationRepository.ExistsAsync(userId, referenceKey, cancellationToken))
            return;

        const string msg = "Tudo certo novamente. Seu pagamento foi confirmado e seu plano premium já está ativo.";
        await notificationRepository.AddRangeAsync(
            [new UserNotification(userId, NotificationKind.BillingReactivated, "Plano reativado", msg, referenceKey,
                payloadJson: $$"""{"checkoutId":"{{checkout.Id}}","planCode":"{{checkout.PlanCode}}"}""")],
            cancellationToken);
        await notificationRepository.SaveChangesAsync(cancellationToken);
        await TrySendEmailAsync(user.Email,
            "Seu acesso premium está restaurado",
            BuildEmailHtml("Acesso premium restaurado", msg, $"{FrontendBase}/dashboard", "Acessar o app"),
            cancellationToken);
    }

    public async Task NotifyGracePeriodReminderAsync(Guid userId, string planCode, DateTime gracePeriodEndsAtUtc, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        var referenceKey = $"billing:{userId}:{NotificationKind.BillingGracePeriodReminder}:{gracePeriodEndsAtUtc:yyyyMMdd}";
        if (await notificationRepository.ExistsAsync(userId, referenceKey, cancellationToken))
            return;

        var endsLabel = gracePeriodEndsAtUtc.ToString("dd/MM/yyyy");
        var msg = $"Seu acesso premium está quase sendo interrompido. Regularize o pagamento até {endsLabel} para continuar usando seu plano sem perder o ritmo.";
        await notificationRepository.AddRangeAsync(
            [new UserNotification(userId, NotificationKind.BillingGracePeriodReminder, "Acesso premium em risco", msg, referenceKey,
                payloadJson: $$"""{"planCode":"{{planCode}}","gracePeriodEndsAt":"{{gracePeriodEndsAtUtc:O}}"}""")],
            cancellationToken);
        await notificationRepository.SaveChangesAsync(cancellationToken);
        await TrySendEmailAsync(user.Email,
            "Seu acesso premium está em risco",
            BuildEmailHtml("Acesso premium em risco", msg, $"{FrontendBase}/assinatura", "Atualizar pagamento"),
            cancellationToken);
    }

    public async Task NotifyDowngradedAsync(Guid userId, string planCode, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId, cancellationToken);
        var referenceKey = $"billing:{userId}:{NotificationKind.BillingDowngraded}:{DateTime.UtcNow:yyyyMMdd}";
        if (await notificationRepository.ExistsAsync(userId, referenceKey, cancellationToken))
            return;

        const string msg = "Como não conseguimos confirmar o pagamento, seu acesso voltou para o plano Essencial. Você pode reativar seu plano premium a qualquer momento.";
        await notificationRepository.AddRangeAsync(
            [new UserNotification(userId, NotificationKind.BillingDowngraded, "Plano atualizado para Essencial", msg, referenceKey,
                payloadJson: $$"""{"planCode":"{{planCode}}"}""")],
            cancellationToken);
        await notificationRepository.SaveChangesAsync(cancellationToken);
        await TrySendEmailAsync(user.Email,
            "Acesso premium encerrado",
            BuildEmailHtml("Plano atualizado para Essencial", msg, $"{FrontendBase}/assinatura", "Reativar plano"),
            cancellationToken);
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

    private static string PlanDisplayName(string planCode) => planCode.ToLowerInvariant() switch
    {
        "basic" => "Basic",
        "intermediate" => "Intermediate",
        "advanced" => "Advanced",
        _ => planCode
    };

    private static string BuildEmailHtml(string title, string body, string? ctaUrl = null, string? ctaLabel = null)
    {
        var ctaBlock = ctaUrl is not null
            ? $"""<p style="margin-top:28px;"><a href="{ctaUrl}" style="background:#1d4ed8;color:#ffffff;padding:12px 28px;border-radius:6px;text-decoration:none;font-weight:bold;font-size:15px;">{ctaLabel ?? "Acessar o app"}</a></p>"""
            : string.Empty;
        return $"""
                <html lang="pt-BR">
                <body style="font-family:Arial,sans-serif;color:#0f172a;max-width:560px;margin:0 auto;padding:32px 16px;">
                  <h2 style="color:#1d4ed8;margin-bottom:8px;font-size:20px;">{title}</h2>
                  <p style="font-size:15px;line-height:1.6;margin-top:8px;">{body}</p>
                  {ctaBlock}
                  <hr style="border:none;border-top:1px solid #e2e8f0;margin:40px 0 16px;" />
                  <p style="font-size:12px;color:#64748b;margin:0;">Investindo em Negócios · Você recebeu este e-mail porque tem uma conta ativa.</p>
                </body>
                </html>
                """;
    }
}
