using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Infrastructure.Auth;

public interface IAuthCookieService
{
    void SetAuthCookies(HttpResponse response, string accessToken, DateTime accessExpiresAtUtc, string refreshToken, DateTime refreshExpiresAtUtc);

    void SetCsrfCookie(HttpResponse response);

    void ClearAuthCookies(HttpResponse response);
}

public sealed class AuthCookieOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// true (padrão/produção): Secure=true + SameSite=None — requer HTTPS.
    /// false (dev em HTTP): Secure=false + SameSite=Lax — funciona com IP puro sem TLS,
    /// desde que frontend e backend compartilhem o mesmo host (mesmo IP, portas distintas
    /// ainda são consideradas same-site pelo browser).
    /// </summary>
    public bool RequireSecureCookie { get; set; } = true;
}

public sealed class AuthCookieService : IAuthCookieService
{
    public const string AccessTokenCookie = "access_token";
    public const string RefreshTokenCookie = "refresh_token";
    public const string CsrfCookie = "XSRF-TOKEN";
    public const string CsrfHeader = "X-XSRF-TOKEN";

    private readonly bool _secure;
    private readonly SameSiteMode _sameSite;

    public AuthCookieService(IOptions<AuthCookieOptions> options)
    {
        _secure = options.Value.RequireSecureCookie;
        _sameSite = _secure ? SameSiteMode.None : SameSiteMode.Lax;
    }

    public void SetAuthCookies(HttpResponse response, string accessToken, DateTime accessExpiresAtUtc, string refreshToken, DateTime refreshExpiresAtUtc)
    {
        response.Cookies.Append(AccessTokenCookie, accessToken, BuildOptions(httpOnly: true, accessExpiresAtUtc));
        response.Cookies.Append(RefreshTokenCookie, refreshToken, BuildOptions(httpOnly: true, refreshExpiresAtUtc));
    }

    public void SetCsrfCookie(HttpResponse response)
    {
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

    private CookieOptions BuildOptions(bool httpOnly, DateTime expiresAtUtc) => new()
    {
        HttpOnly = httpOnly,
        Secure = _secure,
        SameSite = _sameSite,
        Path = "/",
        Expires = DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc)
    };
}
