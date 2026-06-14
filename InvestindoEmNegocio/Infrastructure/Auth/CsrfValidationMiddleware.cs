using Microsoft.AspNetCore.Http;

namespace InvestindoEmNegocio.Infrastructure.Auth;

public sealed class CsrfValidationMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> MutatingMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete
    };

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!MutatingMethods.Contains(context.Request.Method)
            || path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v1/auth", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (context.Request.Cookies.TryGetValue(AuthCookieService.CsrfCookie, out var cookieToken) && !string.IsNullOrEmpty(cookieToken))
        {
            var headerToken = context.Request.Headers[AuthCookieService.CsrfHeader].ToString();
            if (string.IsNullOrEmpty(headerToken) || !string.Equals(headerToken, cookieToken, StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    title = "CSRF inválido",
                    detail = "Cabeçalho X-XSRF-TOKEN ausente ou inválido.",
                    status = StatusCodes.Status403Forbidden
                });
                return;
            }
        }

        await next(context);
    }
}
