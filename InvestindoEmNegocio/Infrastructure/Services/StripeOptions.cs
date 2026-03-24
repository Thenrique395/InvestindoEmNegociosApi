namespace InvestindoEmNegocio.Infrastructure.Services;

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string FrontendBaseUrl { get; set; } = "http://localhost:4200";
    public string PortalReturnPath { get; set; } = "/assinatura";
    public string SuccessPath { get; set; } = "/checkout/sucesso";
    public string CancelPath { get; set; } = "/checkout/pendente";
    public string FailedPath { get; set; } = "/checkout/falha";
    public string[] PaymentMethodTypes { get; set; } = ["card"];
}
