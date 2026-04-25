using System.Text.Json;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Application.Services;

public sealed class DataPortabilityApplicationService(
    IDataPortabilityService dataPortabilityService,
    IDataPortabilityGuardService dataPortabilityGuardService,
    ILogger<DataPortabilityApplicationService> logger) : IDataPortabilityApplicationService
{
    public async Task<(string FileName, byte[] Content)> ExportAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        dataPortabilityGuardService.EnsureEnabled();
        return await dataPortabilityService.ExportAsync(userId, cancellationToken);
    }

    public async Task<ImportUserDataResult> ImportAsync(
        Guid userId,
        Stream stream,
        long fileLength,
        bool replaceExisting,
        CancellationToken cancellationToken = default)
    {
        dataPortabilityGuardService.EnsureEnabled();
        dataPortabilityGuardService.ValidateImportFile(userId, fileLength);

        try
        {
            return await dataPortabilityService.ImportAsync(userId, stream, replaceExisting, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Invalid import para {UserId}", userId);
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
}
