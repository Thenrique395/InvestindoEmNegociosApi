namespace InvestindoEmNegocio.Domain.Common;

/// <summary>
/// Mascaramento de PII para uso em logs/observabilidade — preserva legibilidade para
/// suporte (domínio visível, início do nome local) sem expor o dado completo em texto claro.
/// </summary>
public static class LogMasking
{
    public static string Email(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "(vazio)";

        var at = email.IndexOf('@');
        if (at <= 0)
            return "***";

        var local = email[..at];
        var domain = email[(at + 1)..];
        var visible = local.Length <= 2 ? local[..1] : local[..2];
        return $"{visible}***@{domain}";
    }
}
