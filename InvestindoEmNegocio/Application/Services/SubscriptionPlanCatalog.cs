using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Security;

namespace InvestindoEmNegocio.Application.Services;

internal sealed record SubscriptionPlanDefinition(
    string Code,
    string Name,
    UserRole Role,
    string Description,
    decimal MonthlyPrice,
    decimal YearlyPrice,
    bool Recommended,
    IReadOnlyList<string> Features,
    IReadOnlyDictionary<string, string> Limits);

internal static class SubscriptionPlanCatalog
{
    public static readonly IReadOnlyList<SubscriptionPlanDefinition> Plans =
    [
        new(
            "basic",
            "Basic",
            UserRole.Basic,
            "Base gratuita para controle financeiro essencial.",
            0m,
            0m,
            false,
            [
                "Dashboard essencial, despesas, receitas, metas simples e categorias padrão",
                "Centro de privacidade e exportação de dados",
                "Conta principal automática para leitura básica"
            ],
            new Dictionary<string, string>
            {
                ["contas"] = "1 conta principal sem gestão avançada",
                ["cartoes"] = "não incluído",
                ["importacao-fatura"] = "não incluída",
                ["investimentos"] = "não incluído"
            }),
        new(
            "intermediate",
            "Intermediate",
            UserRole.Intermediate,
            "Para rotina mensal com calendário, categorias e importação de fatura.",
            29.90m,
            299m,
            true,
            [
                "Tudo do Basic",
                "Contas e cartões gerenciáveis",
                "Calendário financeiro",
                "Categorias personalizadas",
                "Importação OFX/CSV/fatura e workflows avançados"
            ],
            new Dictionary<string, string>
            {
                ["contas"] = "até 5 contas",
                ["cartoes"] = "até 8 cartões",
                ["importacao-fatura"] = "incluída",
                ["investimentos"] = "não incluído"
            }),
        new(
            "advanced",
            "Advanced",
            UserRole.Advanced,
            "Camada patrimonial e de investimentos para operação completa.",
            59.90m,
            599m,
            false,
            [
                "Tudo do Intermediate",
                "Investimentos e wealth",
                "Recorrência, insights e recomendação em nível avançado",
                "Planejamento patrimonial ampliado"
            ],
            new Dictionary<string, string>
            {
                ["contas"] = "ilimitadas",
                ["cartoes"] = "ilimitados",
                ["importacao-fatura"] = "incluída",
                ["investimentos"] = "incluído"
            })
    ];

    public static SubscriptionPlanDefinition GetByCodeOrThrow(string code)
    {
        var plan = Plans.FirstOrDefault(x => string.Equals(x.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));
        return plan ?? throw new ArgumentException("Plano informado não é válido.");
    }

    public static IReadOnlyList<string> BuildFeatureSummary(UserRole role)
    {
        return AppFeatureMatrix.GetRoleFeatures(role)
            .OrderBy(x => x)
            .ToList();
    }
}
