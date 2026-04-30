using System.Text.Json;
using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class DataPortabilityApplicationServiceTests
{
    [Fact]
    public async Task ExportAsync_Should_Throw_404_When_Feature_Is_Disabled()
    {
        var sut = BuildService(enabled: false, exportService: new Mock<IDataPortabilityExportService>(), importService: new Mock<IDataPortabilityImportService>());

        Func<Task> act = async () => await sut.ExportAsync(Guid.NewGuid());

        var ex = await act.Should().ThrowAsync<AppProblemException>();
        ex.Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        ex.Which.Title.Should().Be("Funcionalidade desabilitada");
    }

    [Fact]
    public async Task ImportAsync_Should_Throw_400_When_File_Length_Is_Invalid()
    {
        var sut = BuildService(enabled: true, exportService: new Mock<IDataPortabilityExportService>(), importService: new Mock<IDataPortabilityImportService>());
        await using var stream = new MemoryStream([1, 2, 3]);

        Func<Task> act = async () => await sut.ImportAsync(Guid.NewGuid(), stream, 0, replaceExisting: false);

        var ex = await act.Should().ThrowAsync<AppProblemException>();
        ex.Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ex.Which.Title.Should().Be("Arquivo inválido");
    }

    [Fact]
    public async Task ImportAsync_Should_Map_JsonException_To_400()
    {
        var importService = new Mock<IDataPortabilityImportService>();
        importService
            .Setup(x => x.ImportAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new JsonException("json inválido"));
        var sut = BuildService(enabled: true, exportService: new Mock<IDataPortabilityExportService>(), importService: importService, maxImportMb: 1);
        await using var stream = new MemoryStream([1]);

        Func<Task> act = async () => await sut.ImportAsync(Guid.NewGuid(), stream, 1, replaceExisting: true);

        var ex = await act.Should().ThrowAsync<AppProblemException>();
        ex.Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ex.Which.Title.Should().Be("Arquivo JSON inválido");
    }

    [Fact]
    public async Task ImportAsync_Should_Map_DbUpdateException_To_400()
    {
        var importService = new Mock<IDataPortabilityImportService>();
        importService
            .Setup(x => x.ImportAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("db fail"));
        var sut = BuildService(enabled: true, exportService: new Mock<IDataPortabilityExportService>(), importService: importService, maxImportMb: 1);
        await using var stream = new MemoryStream([1]);

        Func<Task> act = async () => await sut.ImportAsync(Guid.NewGuid(), stream, 1, replaceExisting: false);

        var ex = await act.Should().ThrowAsync<AppProblemException>();
        ex.Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ex.Which.Title.Should().Be("Falha ao importar dados");
    }

    [Fact]
    public async Task ImportAsync_Should_Return_Result_When_Valid()
    {
        var expected = new ImportUserDataResult(12);
        var importService = new Mock<IDataPortabilityImportService>();
        importService
            .Setup(x => x.ImportAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var sut = BuildService(enabled: true, exportService: new Mock<IDataPortabilityExportService>(), importService: importService, maxImportMb: 10);
        await using var stream = new MemoryStream([1, 2]);

        var result = await sut.ImportAsync(Guid.NewGuid(), stream, fileLength: 2, replaceExisting: true);

        result.Should().BeEquivalentTo(expected);
    }

    private static DataPortabilityApplicationService BuildService(
        bool enabled,
        Mock<IDataPortabilityExportService> exportService,
        Mock<IDataPortabilityImportService> importService,
        int maxImportMb = 20)
    {
        var options = Options.Create(new DataPortabilityOptions
        {
            Enabled = enabled,
            MaxImportSizeMb = maxImportMb
        });
        var guard = new DataPortabilityGuardService(options, NullLogger<DataPortabilityGuardService>.Instance);

        return new DataPortabilityApplicationService(exportService.Object, importService.Object, guard, NullLogger<DataPortabilityApplicationService>.Instance);
    }
}
