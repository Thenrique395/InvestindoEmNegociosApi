using System.Text.Json;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Tests;

public class DataPortabilityFacadeServiceTests
{
    [Fact]
    public async Task ExportAsync_Should_Throw_404_When_Feature_Is_Disabled()
    {
        var sut = BuildService(enabled: false);

        var ex = await Assert.ThrowsAsync<AppProblemException>(() => sut.ExportAsync(Guid.NewGuid()));

        Assert.Equal(StatusCodes.Status404NotFound, ex.StatusCode);
        Assert.Equal("Funcionalidade desabilitada", ex.Title);
    }

    [Fact]
    public async Task ImportAsync_Should_Throw_400_When_File_Length_Is_Invalid()
    {
        var sut = BuildService(enabled: true);
        await using var stream = new MemoryStream([1, 2, 3]);

        var ex = await Assert.ThrowsAsync<AppProblemException>(() =>
            sut.ImportAsync(Guid.NewGuid(), stream, 0, replaceExisting: false));

        Assert.Equal(StatusCodes.Status400BadRequest, ex.StatusCode);
        Assert.Equal("Arquivo inválido", ex.Title);
    }

    [Fact]
    public async Task ImportAsync_Should_Map_JsonException_To_400()
    {
        var portability = new FakeDataPortabilityService
        {
            OnImportAsync = (_, _, _, _) => Task.FromException<ImportUserDataResult>(new JsonException("json inválido"))
        };
        var sut = BuildService(enabled: true, maxImportMb: 1, portability);
        await using var stream = new MemoryStream([1]);

        var ex = await Assert.ThrowsAsync<AppProblemException>(() =>
            sut.ImportAsync(Guid.NewGuid(), stream, 1, replaceExisting: true));

        Assert.Equal(StatusCodes.Status400BadRequest, ex.StatusCode);
        Assert.Equal("Arquivo JSON inválido", ex.Title);
    }

    [Fact]
    public async Task ImportAsync_Should_Map_DbUpdateException_To_400()
    {
        var portability = new FakeDataPortabilityService
        {
            OnImportAsync = (_, _, _, _) => Task.FromException<ImportUserDataResult>(new DbUpdateException("db fail"))
        };
        var sut = BuildService(enabled: true, maxImportMb: 1, portability);
        await using var stream = new MemoryStream([1]);

        var ex = await Assert.ThrowsAsync<AppProblemException>(() =>
            sut.ImportAsync(Guid.NewGuid(), stream, 1, replaceExisting: false));

        Assert.Equal(StatusCodes.Status400BadRequest, ex.StatusCode);
        Assert.Equal("Falha ao importar dados", ex.Title);
    }

    [Fact]
    public async Task ImportAsync_Should_Return_Result_When_Valid()
    {
        var expected = new ImportUserDataResult(12);
        var portability = new FakeDataPortabilityService
        {
            OnImportAsync = (_, _, _, _) => Task.FromResult(expected)
        };
        var sut = BuildService(enabled: true, maxImportMb: 10, portability);
        await using var stream = new MemoryStream([1, 2]);

        var result = await sut.ImportAsync(Guid.NewGuid(), stream, fileLength: 2, replaceExisting: true);

        Assert.Equal(expected.ImportedRecords, result.ImportedRecords);
    }

    private static DataPortabilityFacadeService BuildService(
        bool enabled,
        int maxImportMb = 20,
        FakeDataPortabilityService? portability = null)
    {
        portability ??= new FakeDataPortabilityService();
        var options = Options.Create(new DataPortabilityOptions
        {
            Enabled = enabled,
            MaxImportSizeMb = maxImportMb
        });

        return new DataPortabilityFacadeService(portability, options, NullLogger<DataPortabilityFacadeService>.Instance);
    }

    private sealed class FakeDataPortabilityService : IDataPortabilityService
    {
        public Func<Guid, CancellationToken, Task<(string FileName, byte[] Content)>>? OnExportAsync { get; init; }
        public Func<Guid, Stream, bool, CancellationToken, Task<ImportUserDataResult>>? OnImportAsync { get; init; }

        public Task<(string FileName, byte[] Content)> ExportAsync(Guid userId, CancellationToken cancellationToken = default) =>
            OnExportAsync?.Invoke(userId, cancellationToken) ?? Task.FromResult<(string FileName, byte[] Content)>(("export.json", []));

        public Task<ImportUserDataResult> ImportAsync(
            Guid userId,
            Stream stream,
            bool replaceExisting,
            CancellationToken cancellationToken = default) =>
            OnImportAsync?.Invoke(userId, stream, replaceExisting, cancellationToken) ?? Task.FromResult(new ImportUserDataResult(1));
    }
}
