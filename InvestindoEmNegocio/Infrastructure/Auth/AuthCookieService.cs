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
        var secure = response.HttpContext.Request.IsHttps;
        response.Cookies.Append(AccessTokenCookie, accessToken, BuildOptions(secure, httpOnly: true, accessExpiresAtUtc));
        response.Cookies.Append(RefreshTokenCookie, refreshToken, BuildOptions(secure, httpOnly: true, refreshExpiresAtUtc));
    }

    public void SetCsrfCookie(HttpResponse response)
    {
        var secure = response.HttpContext.Request.IsHttps;
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        response.Cookies.Append(CsrfCookie, token, BuildOptions(secure, httpOnly: false, DateTime.UtcNow.AddDays(30)));
    }

    public void ClearAuthCookies(HttpResponse response)
    {
        var secure = response.HttpContext.Request.IsHttps;
        var expired = BuildOptions(secure, httpOnly: true, DateTime.UnixEpoch);
        response.Cookies.Delete(AccessTokenCookie, expired);
        response.Cookies.Delete(RefreshTokenCookie, expired);
        response.Cookies.Delete(CsrfCookie, BuildOptions(secure, httpOnly: false, DateTime.UnixEpoch));
    }

    private static CookieOptions BuildOptions(bool secure, bool httpOnly, DateTime expiresAtUtc) => new()
    {
        HttpOnly = httpOnly,
        Secure = secure,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Expires = DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc)
    };
}
