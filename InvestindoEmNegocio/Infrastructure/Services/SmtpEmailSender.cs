using System.Net;
using System.Net.Mail;
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
            From = new MailAddress(settings.FromEmail, settings.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(to);

        if (!string.IsNullOrWhiteSpace(textBody))
        {
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(textBody, null, "text/plain"));
        }

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
