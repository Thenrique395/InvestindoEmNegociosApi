namespace InvestindoEmNegocio.Application.Services;

public sealed class MercadoPagoOptions
{
    public const string SectionName = "MercadoPago";

    public string AccessToken { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string FrontendBaseUrl { get; set; } = "http://localhost:4200";
    public string SuccessPath { get; set; } = "/checkout/sucesso";
    public string PendingPath { get; set; } = "/checkout/pendente";
    public string FailedPath { get; set; } = "/checkout/falha";

    /// <summary>
    /// URL base da API do Mercado Pago. Configurável só para permitir apontar a um
    /// mock/sandbox alternativo em teste; em produção usa o padrão.
    /// </summary>
    public string ApiBaseUrl { get; set; } = "https://api.mercadopago.com";
}
