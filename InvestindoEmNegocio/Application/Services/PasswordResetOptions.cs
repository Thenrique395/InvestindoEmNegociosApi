namespace InvestindoEmNegocio.Application.Services;

public sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    public string FrontendResetUrl { get; set; } = "http://localhost:4200/reset-password";
    public int TokenExpiryMinutes { get; set; } = 30;
}
