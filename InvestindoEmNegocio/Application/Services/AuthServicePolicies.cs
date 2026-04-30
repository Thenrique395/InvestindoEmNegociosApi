namespace InvestindoEmNegocio.Application.Services;

internal static class AuthServicePolicies
{
    public const int BcryptWorkFactor = 12;
    public const int MaxFailedLoginAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
