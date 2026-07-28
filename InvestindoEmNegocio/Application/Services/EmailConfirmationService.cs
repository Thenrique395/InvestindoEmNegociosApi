using System.Net;
using System.Security.Cryptography;
using System.Text;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Common;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Application.Services;

// Confirmação de e-mail (double opt-in). Espelha PasswordResetService: token aleatório enviado por
// e-mail, guardado só como HASH (SHA-256), com validade e uso único.
public sealed class EmailConfirmationService(
    IUserRepository userRepository,
    IEmailConfirmationTokenRepository tokenRepository,
    IEmailSender emailSender,
    IOptions<EmailConfirmationOptions> options,
    ILogger<EmailConfirmationService> logger) : IEmailConfirmationService
{
    public async Task SendConfirmationAsync(User user, CancellationToken cancellationToken = default)
    {
        var rawToken = GenerateToken();
        var tokenHash = HashToken(rawToken);
        var now = DateTime.UtcNow;
        var ttlMinutes = Math.Clamp(options.Value.TokenExpiryMinutes, 30, 4320); // 30min .. 3 dias
        var expiresAt = now.AddMinutes(ttlMinutes);

        await tokenRepository.AddAsync(new EmailConfirmationToken(user.Id, tokenHash, expiresAt), cancellationToken);
        await tokenRepository.SaveChangesAsync(cancellationToken);

        var link = BuildConfirmLink(rawToken);
        var subject = "Confirme seu e-mail";
        var safeName = WebUtility.HtmlEncode(user.Name);
        var safeLink = WebUtility.HtmlEncode(link);
        var hours = Math.Max(1, ttlMinutes / 60);
        var html = $"""
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f3f5f9;padding:24px 0;font-family:Arial,Helvetica,sans-serif;">
                      <tr>
                        <td align="center">
                          <table role="presentation" width="620" cellspacing="0" cellpadding="0" style="max-width:620px;background:#ffffff;border:1px solid #e5e7eb;border-radius:14px;overflow:hidden;">
                            <tr>
                              <td style="padding:20px 24px;background:#f8fafc;border-bottom:1px solid #e5e7eb;">
                                <div style="font-size:12px;letter-spacing:1.8px;text-transform:uppercase;color:#64748b;font-weight:700;">Investindo em Negocios</div>
                                <div style="margin-top:8px;font-size:24px;line-height:1.3;color:#0f172a;font-weight:700;">Confirme seu e-mail</div>
                              </td>
                            </tr>
                            <tr>
                              <td style="padding:24px;">
                                <p style="margin:0 0 14px;font-size:15px;line-height:1.6;color:#334155;">Ola <strong>{safeName}</strong>,</p>
                                <p style="margin:0 0 14px;font-size:15px;line-height:1.6;color:#334155;">Falta so um passo para ativar sua conta.</p>
                                <p style="margin:0 0 18px;font-size:15px;line-height:1.6;color:#334155;">Clique no botao abaixo para confirmar seu e-mail:</p>
                                <table role="presentation" cellspacing="0" cellpadding="0" style="margin:0 0 18px;">
                                  <tr>
                                    <td align="center" bgcolor="#2563eb" style="border-radius:10px;">
                                      <a href="{safeLink}" style="display:inline-block;padding:12px 18px;font-size:14px;font-weight:700;color:#ffffff;text-decoration:none;">Confirmar meu e-mail</a>
                                    </td>
                                  </tr>
                                </table>
                                <p style="margin:0 0 8px;font-size:13px;line-height:1.6;color:#475569;">Este link expira em <strong>{hours} horas</strong>.</p>
                                <p style="margin:0 0 8px;font-size:13px;line-height:1.6;color:#475569;">Se o botao nao funcionar, copie e cole este link no navegador:</p>
                                <p style="margin:0 0 18px;word-break:break-all;font-size:12px;line-height:1.5;color:#2563eb;">{safeLink}</p>
                                <p style="margin:0;font-size:12px;line-height:1.6;color:#64748b;">Se voce nao criou esta conta, pode ignorar esta mensagem com seguranca.</p>
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>
                    </table>
                    """;
        var text = $"Ola {user.Name},\n\nConfirme seu e-mail para ativar sua conta usando este link: {link}\n\nEste link expira em {hours} horas.\n\nSe voce nao criou esta conta, ignore esta mensagem.";

        try
        {
            await emailSender.SendAsync(user.Email, subject, html, text, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send confirmation email for {UserId}", user.Id);
        }

        logger.LogInformation("Email confirmation token issued {UserId}", user.Id);
    }

    public async Task ConfirmAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new UnauthorizedAccessException("Token de confirmação inválido ou expirado.");

        var now = DateTime.UtcNow;
        var tokenHash = HashToken(token);
        var stored = await tokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (stored is null || stored.IsUsed || stored.IsExpired(now))
            throw new UnauthorizedAccessException("Token de confirmação inválido ou expirado.");

        var user = await userRepository.GetByIdAsync(stored.UserId, cancellationToken);
        if (user is null)
            throw new UnauthorizedAccessException("Usuário não encontrado.");

        user.ConfirmEmail();
        stored.MarkAsUsed(now);
        await userRepository.SaveChangesAsync(cancellationToken);
        await tokenRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Email confirmed {UserId}", user.Id);
    }

    public async Task ResendAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByEmailAsync(AuthServicePolicies.NormalizeEmail(email), cancellationToken);
        if (user is null || user.EmailConfirmed)
        {
            logger.LogInformation("Resend confirmation ignored for {Email}", LogMasking.Email(email));
            return;
        }

        await SendConfirmationAsync(user, cancellationToken);
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    private string BuildConfirmLink(string token)
    {
        var baseUrl = options.Value.FrontendConfirmUrl?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = "http://localhost:4200/confirmar-email";

        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{baseUrl}{separator}token={Uri.EscapeDataString(token)}";
    }
}
