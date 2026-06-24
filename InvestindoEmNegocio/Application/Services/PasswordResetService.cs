using System.Net;
using System.Security.Cryptography;
using System.Text;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Common;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Application.Services;

using BCryptNet = BCrypt.Net.BCrypt;

public sealed class PasswordResetService(
    IUserRepository userRepository,
    IPasswordResetTokenRepository passwordResetTokenRepository,
    IUserSessionService userSessionService,
    IEmailSender emailSender,
    IOptions<PasswordResetOptions> passwordResetOptions,
    ILogger<PasswordResetService> logger) : IPasswordResetService
{
    private const int BcryptWorkFactor = 12;
    private const int MinPasswordLength = 8;

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null)
        {
            logger.LogInformation("Password reset requested for unknown email {Email}", LogMasking.Email(request.Email));
            return;
        }

        var rawToken = GeneratePasswordResetToken();
        var tokenHash = HashToken(rawToken);
        var now = DateTime.UtcNow;
        var ttlMinutes = Math.Clamp(passwordResetOptions.Value.TokenExpiryMinutes, 5, 120);
        var expiresAt = now.AddMinutes(ttlMinutes);

        await passwordResetTokenRepository.AddAsync(new PasswordResetToken(user.Id, tokenHash, expiresAt), cancellationToken);
        await passwordResetTokenRepository.SaveChangesAsync(cancellationToken);

        var resetLink = BuildResetLink(rawToken);
        var subject = "Recuperacao de senha";
        var safeName = WebUtility.HtmlEncode(user.Name);
        var safeLink = WebUtility.HtmlEncode(resetLink);
        var html = $"""
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f3f5f9;padding:24px 0;font-family:Arial,Helvetica,sans-serif;">
                      <tr>
                        <td align="center">
                          <table role="presentation" width="620" cellspacing="0" cellpadding="0" style="max-width:620px;background:#ffffff;border:1px solid #e5e7eb;border-radius:14px;overflow:hidden;">
                            <tr>
                              <td style="padding:20px 24px;background:#f8fafc;border-bottom:1px solid #e5e7eb;">
                                <div style="font-size:12px;letter-spacing:1.8px;text-transform:uppercase;color:#64748b;font-weight:700;">Investindo em Negocios</div>
                                <div style="margin-top:8px;font-size:24px;line-height:1.3;color:#0f172a;font-weight:700;">Redefinicao de senha</div>
                              </td>
                            </tr>
                            <tr>
                              <td style="padding:24px;">
                                <p style="margin:0 0 14px;font-size:15px;line-height:1.6;color:#334155;">Ola <strong>{safeName}</strong>,</p>
                                <p style="margin:0 0 14px;font-size:15px;line-height:1.6;color:#334155;">Recebemos uma solicitacao para redefinir sua senha.</p>
                                <p style="margin:0 0 18px;font-size:15px;line-height:1.6;color:#334155;">Para continuar, clique no botao abaixo:</p>
                                <table role="presentation" cellspacing="0" cellpadding="0" style="margin:0 0 18px;">
                                  <tr>
                                    <td align="center" bgcolor="#2563eb" style="border-radius:10px;">
                                      <a href="{safeLink}" style="display:inline-block;padding:12px 18px;font-size:14px;font-weight:700;color:#ffffff;text-decoration:none;">Redefinir minha senha</a>
                                    </td>
                                  </tr>
                                </table>
                                <p style="margin:0 0 8px;font-size:13px;line-height:1.6;color:#475569;">Este link expira em <strong>{ttlMinutes} minutos</strong>.</p>
                                <p style="margin:0 0 8px;font-size:13px;line-height:1.6;color:#475569;">Se o botao nao funcionar, copie e cole este link no navegador:</p>
                                <p style="margin:0 0 18px;word-break:break-all;font-size:12px;line-height:1.5;color:#2563eb;">{safeLink}</p>
                                <p style="margin:0;font-size:12px;line-height:1.6;color:#64748b;">Se voce nao solicitou esta alteracao, pode ignorar esta mensagem com seguranca.</p>
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>
                    </table>
                    """;
        var text = $"Ola {user.Name},\n\nRecebemos uma solicitacao para redefinir sua senha.\n\nUse este link: {resetLink}\n\nEste link expira em {ttlMinutes} minutos.\n\nSe voce nao solicitou esta alteracao, ignore esta mensagem.";

        try
        {
            await emailSender.SendAsync(user.Email, subject, html, text, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send password reset email for {UserId}", user.Id);
        }

        logger.LogInformation("Password reset token issued {UserId}", user.Id);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (request.NewPassword.Length < MinPasswordLength)
            throw new ArgumentException($"Nova senha deve ter no mínimo {MinPasswordLength} caracteres.");

        var now = DateTime.UtcNow;
        var tokenHash = HashToken(request.Token);
        var stored = await passwordResetTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (stored is null || stored.IsUsed || stored.IsExpired(now))
            throw new UnauthorizedAccessException("Token de recuperação inválido ou expirado.");

        var user = await userRepository.GetByIdAsync(stored.UserId, cancellationToken);
        if (user is null)
            throw new UnauthorizedAccessException("Usuário não encontrado.");

        var newHash = BCryptNet.HashPassword(request.NewPassword, BcryptWorkFactor);
        user.ChangePassword(newHash);
        user.RevokeSessions();
        stored.MarkAsUsed(now);
        await userRepository.SaveChangesAsync(cancellationToken);
        await passwordResetTokenRepository.SaveChangesAsync(cancellationToken);

        await userSessionService.RevokeActiveAsync(user.Id, now, cancellationToken);
        logger.LogInformation("Password reset completed {UserId}", user.Id);
    }

    private static string GeneratePasswordResetToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    private string BuildResetLink(string token)
    {
        var baseUrl = passwordResetOptions.Value.FrontendResetUrl?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = "http://localhost:4200/reset-password";

        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{baseUrl}{separator}token={Uri.EscapeDataString(token)}";
    }
}
