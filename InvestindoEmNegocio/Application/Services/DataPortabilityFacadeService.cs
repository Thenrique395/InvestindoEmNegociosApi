using System.Text.Json;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Application.Services;

public sealed class DataPortabilityFacadeService(
    IDataPortabilityService dataPortabilityService,
    IOptions<DataPortabilityOptions> options,
    ILogger<DataPortabilityFacadeService> logger) : IDataPortabilityFacadeService
{
    public async Task<(string FileName, byte[] Content)> ExportAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        return await dataPortabilityService.ExportAsync(userId, cancellationToken);
    }

    public async Task<ImportUserDataResult> ImportAsync(
        Guid userId,
        Stream stream,
        long fileLength,
        bool replaceExisting,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        if (fileLength <= 0)
        {
            logger.LogWarning("Importação inválida: arquivo vazio para {UserId}", userId);
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

        try
        {
            return await dataPortabilityService.ImportAsync(userId, stream, replaceExisting, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Importação inválida para {UserId}", userId);
            throw new AppProblemException("Arquivo de importação inválido", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (JsonException)
        {
            logger.LogWarning("JSON inválido na importação para {UserId}", userId);
            throw new AppProblemException("Arquivo JSON inválido", "Não foi possível ler o conteúdo do arquivo enviado.", StatusCodes.Status400BadRequest);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Falha de persistência na importação para {UserId}", userId);
            throw new AppProblemException("Falha ao importar dados", ex.InnerException?.Message ?? ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private void EnsureEnabled()
    {
        if (options.Value.Enabled)
        {
            return;
        }

        logger.LogInformation("Data portability desabilitado por configuração.");
        throw new AppProblemException(
            "Funcionalidade desabilitada",
            "A exportação/importação de dados está desativada.",
            StatusCodes.Status404NotFound);
    }
}
