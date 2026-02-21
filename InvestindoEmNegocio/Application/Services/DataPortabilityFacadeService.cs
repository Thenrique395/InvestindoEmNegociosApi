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
    IOptions<DataPortabilityOptions> options) : IDataPortabilityFacadeService
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
            throw new AppProblemException("Arquivo inválido", "Envie um arquivo JSON para importação.", StatusCodes.Status400BadRequest);
        }

        var maxBytes = Math.Max(1, options.Value.MaxImportSizeMb) * 1024L * 1024L;
        if (fileLength > maxBytes)
        {
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
            throw new AppProblemException("Arquivo de importação inválido", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (JsonException)
        {
            throw new AppProblemException("Arquivo JSON inválido", "Não foi possível ler o conteúdo do arquivo enviado.", StatusCodes.Status400BadRequest);
        }
        catch (DbUpdateException ex)
        {
            throw new AppProblemException("Falha ao importar dados", ex.InnerException?.Message ?? ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private void EnsureEnabled()
    {
        if (options.Value.Enabled)
        {
            return;
        }

        throw new AppProblemException(
            "Funcionalidade desabilitada",
            "A exportação/importação de dados está desativada.",
            StatusCodes.Status404NotFound);
    }
}
