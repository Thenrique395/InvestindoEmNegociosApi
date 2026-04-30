using System.Text.Encodings.Web;
using System.Text.Json;

namespace InvestindoEmNegocio.Application.Services;

internal static class DataPortabilityShared
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true
    };

    public static string ExportCacheKey(Guid userId) => $"dataportability:export:{userId:N}";

    public static string RequireTrimmedValue(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Arquivo inválido: campo obrigatório ausente ({field}).");

        return value.Trim();
    }
}
