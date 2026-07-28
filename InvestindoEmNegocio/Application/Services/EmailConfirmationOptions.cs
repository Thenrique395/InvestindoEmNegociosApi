namespace InvestindoEmNegocio.Application.Services;

public sealed class EmailConfirmationOptions
{
    public const string SectionName = "EmailConfirmation";

    public string FrontendConfirmUrl { get; set; } = "http://localhost:4200/confirmar-email";
    public int TokenExpiryMinutes { get; set; } = 1440; // 24h
}
