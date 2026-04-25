using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Application.Services;

public sealed class DataPortabilityGuardService(
    IOptions<DataPortabilityOptions> options,
    ILogger<DataPortabilityGuardService> logger) : IDataPortabilityGuardService
{
    public void EnsureEnabled()
    {
        if (options.Value.Enabled)
            return;

        logger.LogInformation("Data portability desabilitado por configuração.");
        throw new AppProblemException(
                "Funcionalidade desabilitada",
                "A exportação e importação de dados está desabilitada.",
                StatusCodes.Status404NotFound);
    }

    public void ValidateImportFile(Guid userId, long fileLength)
    {
        if (fileLength <= 0)
        {
            logger.LogWarning("Invalid import: arquivo vazio para {UserId}", userId);
            throw new AppProblemException("Arquivo inválido", "Envie um arquivo JSON para importação.", StatusCodes.Status400BadRequest);
        }

        var maxBytes = Math.Max(1, options.Value.MaxImportSizeMb) * 1024L * 1024L;
        if (fileLength > maxBytes)
        {
            logger.LogWarning(
                "Importação rejeitada por tamanho para {UserId}. Recebido: {FileLengthBytes}, Limite: {MaxBytes}",
                userId,
                fileLength,
                maxBytes);
            throw new AppProblemException(
                "Arquivo muito grande",
                $"Tamanho máximo permitido: {options.Value.MaxImportSizeMb} MB.",
                StatusCodes.Status400BadRequest);
        }
    }
}
