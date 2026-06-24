namespace InvestindoEmNegocio.Application.Services;

public sealed class BillingOptions
{
    public const string SectionName = "Billing";

    /// <summary>
    /// Gateway usado para checkouts NOVOS ("stripe" ou "mercado_pago"). Assinaturas já
    /// existentes continuam usando o gateway gravado em si (<see cref="Domain.Entities.UserSubscription.Provider"/>),
    /// independente desta flag. Default "stripe" — só trocar para "mercado_pago" depois de
    /// configurar credenciais reais e validar o fluxo em sandbox; sem isso, todo checkout
    /// novo passaria a falhar.
    /// </summary>
    public string PrimaryProvider { get; set; } = "stripe";
}
