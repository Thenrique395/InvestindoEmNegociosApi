using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace InvestindoEmNegocio.Infrastructure.Auth;

public interface IAuthCookieService
{
    void SetAuthCookies(HttpResponse response, string accessToken, DateTime accessExpiresAtUtc, string refreshToken, DateTime refreshExpiresAtUtc);

    void SetCsrfCookie(HttpResponse response);

    void ClearAuthCookies(HttpResponse response);
}

public sealed class AuthCookieService : IAuthCookieService
{
    public const string AccessTokenCookie = "access_token";
    public const string RefreshTokenCookie = "refresh_token";
    public const string CsrfCookie = "XSRF-TOKEN";
    public const string CsrfHeader = "X-XSRF-TOKEN";

    public void SetAuthCookies(HttpResponse response, string accessToken, DateTime accessExpiresAtUtc, string refreshToken, DateTime refreshExpiresAtUtc)
    {
        response.Cookies.Append(AccessTokenCookie, accessToken, BuildOptions(httpOnly: true, accessExpiresAtUtc));
        response.Cookies.Append(RefreshTokenCookie, refreshToken, BuildOptions(httpOnly: true, refreshExpiresAtUtc));
    }

    public void SetCsrfCookie(HttpResponse response)
    {
        // base64url (sem '+', '/', '=') para evitar percent-encoding no Set-Cookie — o
        // servidor decodifica o valor lido via Request.Cookies, mas não decodifica o header
        // X-XSRF-TOKEN, então qualquer caractere que precisasse de escape quebraria a
        // comparação de double-submit cookie no CsrfValidationMiddleware.
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        response.Cookies.Append(CsrfCookie, token, BuildOptions(httpOnly: false, DateTime.UtcNow.AddDays(30)));
    }

    public void ClearAuthCookies(HttpResponse response)
    {
        var expired = BuildOptions(httpOnly: true, DateTime.UnixEpoch);
        response.Cookies.Delete(AccessTokenCookie, expired);
        response.Cookies.Delete(RefreshTokenCookie, expired);
        response.Cookies.Delete(CsrfCookie, BuildOptions(httpOnly: false, DateTime.UnixEpoch));
    }

    // SameSite=None é obrigatório porque frontend e backend rodam em origens diferentes
    // (portas distintas) — SameSite=Lax não é enviado em chamadas fetch/XHR cross-origin,
    // só em navegação top-level. SameSite=None exige Secure=true sempre (não condicional):
    // só funciona em HTTPS real ou em http://localhost (tratado como contexto seguro pelo
    // navegador). Em IP puro sem TLS (ambiente implantado), os cookies não serão enviados
    // até existir HTTPS real lá.
    private static CookieOptions BuildOptions(bool httpOnly, DateTime expiresAtUtc) => new()
    {
        HttpOnly = httpOnly,
        Secure = true,
        SameSite = SameSiteMode.None,
        Path = "/",
        Expires = DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc)
    };
}
