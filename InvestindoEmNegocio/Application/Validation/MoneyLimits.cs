namespace InvestindoEmNegocio.Application.Validation;

/// <summary>
/// Limites de sanidade para valores e parcelas de planos/lançamentos. O teto de
/// valor mantém os montantes dentro de numeric(14,2) do banco (evita erro 500 por
/// overflow) e o teto de parcelas evita geração em massa de linhas.
/// </summary>
public static class MoneyLimits
{
    /// <summary>~1 bilhão — confortavelmente dentro de numeric(14,2).</summary>
    public const decimal MaxAmount = 999_999_999.99m;

    /// <summary>Até 40 anos de parcelas mensais.</summary>
    public const int MaxInstallments = 480;
}
