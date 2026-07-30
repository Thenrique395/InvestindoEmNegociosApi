namespace InvestindoEmNegocio.Domain.Enums;

/// <summary>
/// Período/base da taxa de juros informada — explícito, para nunca se presumir se um valor é
/// mensal ou anual, nem se a taxa anual é nominal (linear) ou efetiva (composta).
/// </summary>
public enum InterestRatePeriod
{
    /// <summary>Taxa anual nominal (linear): mensal = anual / 12. Convenção atual da API.</summary>
    AnnualNominal = 1,
    /// <summary>Taxa anual efetiva (composta): mensal = (1 + anual)^(1/12) − 1.</summary>
    AnnualEffective = 2,
    /// <summary>Taxa informada já é mensal.</summary>
    Monthly = 3
}
