using System.Net.Mail;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace InvestindoEmNegocio.Application.Services;

public sealed class AdminEmailDiagnosticsService(IEmailSender emailSender) : IAdminEmailDiagnosticsService
{
    public async Task<TestEmailResult> SendTestEmailAsync(string to, CancellationToken cancellationToken = default)
    {
        var recipient = (to ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(recipient))
        {
            throw new AppProblemException("Email inválido", "Informe um email de destino para teste.", StatusCodes.Status400BadRequest);
        }

        if (!IsValidEmail(recipient))
        {
            throw new AppProblemException("Email inválido", "Formato de email inválido.", StatusCodes.Status400BadRequest);
        }

        var sentAt = DateTime.UtcNow;
        var subject = "Teste SMTP - Investindo em Negócios";
        var htmlBody = $"""
                        <div style="font-family:Arial,sans-serif;line-height:1.5;color:#0f172a">
                          <h2 style="margin:0 0 12px">Teste de envio SMTP</h2>
                          <p>Este é um e-mail de teste disparado pelo painel administrativo.</p>
                          <p><strong>Horário UTC:</strong> {sentAt:yyyy-MM-dd HH:mm:ss}</p>
                        </div>
                        """;
        var textBody = $"Teste de envio SMTP do painel administrativo. Horário UTC: {sentAt:yyyy-MM-dd HH:mm:ss}.";

        try
        {
            await emailSender.SendAsync(recipient, subject, htmlBody, textBody, cancellationToken);
            return new TestEmailResult(recipient, sentAt);
        }
        catch (Exception ex)
        {
            throw new AppProblemException(
                "Falha no envio de e-mail",
                $"Não foi possível enviar o e-mail de teste. Detalhe: {ex.Message}",
                StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
