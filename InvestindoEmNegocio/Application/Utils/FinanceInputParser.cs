using System.Globalization;

namespace InvestindoEmNegocio.Application.Utils;

public static class FinanceInputParser
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static bool TryParseMoney(string? value, out decimal result)
    {
        result = 0m;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var cleaned = value
            .Replace("R$", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty)
            .Replace(".", string.Empty)
            .Replace(",", ".");

        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }

    public static DateOnly ParseDateOrDefault(string? value, DateOnly fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var trimmed = value.Trim();
        if (DateOnly.TryParseExact(trimmed, "dd/MM/yyyy", PtBr, DateTimeStyles.None, out var full))
            return full;

        if (DateOnly.TryParseExact(trimmed, "dd/MM", PtBr, DateTimeStyles.None, out var monthDay))
            return new DateOnly(fallback.Year, monthDay.Month, monthDay.Day);

        return fallback;
    }
}

