using System.Net;
using System.Net.Mail;
using System.Text;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Common;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Infrastructure.Services;

public sealed class SmtpEmailSender(
    IOptions<SmtpEmailOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody, string? textBody = null, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.FromEmail))
        {
            const string errorMessage = "SMTP não configurado (Host/FromEmail).";
            logger.LogWarning("{Message} E-mail para {Recipient} não enviado.", errorMessage, LogMasking.Email(to));
            throw new InvalidOperationException(errorMessage);
        }

        logger.LogInformation(
            "Sending email using SMTP host {Host}:{Port} as user configured={HasUser}",
            settings.Host,
            settings.Port,
            !string.IsNullOrWhiteSpace(settings.Username));

        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromEmail, settings.FromName, Encoding.UTF8),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8
        };
        message.To.Add(to);

        // multipart/alternative com UTF-8: o texto vem PRIMEIRO e o HTML por ÚLTIMO — os clientes
        // de e-mail preferem a última parte, então mostram o HTML (com botão) e não o texto puro.
        // Sem definir Body/IsBodyHtml para não inverter a ordem (bug que fazia o Gmail mostrar texto).
        if (!string.IsNullOrWhiteSpace(textBody))
        {
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(textBody, Encoding.UTF8, "text/plain"));
        }
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(htmlBody, Encoding.UTF8, "text/html"));

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);
        }
        else
        {
            const string errorMessage = "SMTP não configurado (Username).";
            logger.LogWarning("{Message} E-mail para {Recipient} não enviado.", errorMessage, LogMasking.Email(to));
            throw new InvalidOperationException(errorMessage);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);
    }
}
