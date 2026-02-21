using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class B3SyncServiceTests
{
    [Fact]
    public async Task SyncAsync_Should_Return_None_When_Consent_Is_Missing()
    {
        var sut = BuildSut();

        var result = await sut.SyncAsync(Guid.NewGuid(), new B3SyncRequest(), CancellationToken.None);

        result.Source.Should().Be("none");
        result.Imported.Should().Be(0);
        result.FallbackUsed.Should().BeFalse();
    }

    [Fact]
    public async Task GrantMockConsentAsync_Should_Enable_Consent_Status()
    {
        var userId = Guid.NewGuid();
        var sut = BuildSut();

        await sut.GrantMockConsentAsync(userId, CancellationToken.None);
        var status = await sut.GetConsentStatusAsync(userId, CancellationToken.None);

        status.HasConsent.Should().BeTrue();
        status.Provider.Should().Be("B3");
        status.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task SyncAsync_Should_Use_B3_Api_When_Available()
    {
        var userId = Guid.NewGuid();
        var connector = new Mock<IB3Connector>();
        connector.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        connector
            .Setup(x => x.GetLatestSnapshotAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot());

        var importer = new Mock<IB3ImportService>();
        importer
            .Setup(x => x.ImportSnapshotAsync(userId, It.IsAny<B3ImportSnapshot>(), "merge", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new B3ConfirmImportResponse(3));

        var sut = BuildSut(connector: connector, importService: importer);
        await sut.GrantMockConsentAsync(userId, CancellationToken.None);

        var result = await sut.SyncAsync(userId, new B3SyncRequest("merge"), CancellationToken.None);

        result.Source.Should().Be("b3_api");
        result.Imported.Should().Be(3);
        result.FallbackUsed.Should().BeFalse();
        importer.Verify(x => x.ImportSnapshotAsync(userId, It.IsAny<B3ImportSnapshot>(), "merge", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncAsync_Should_Use_Fallback_When_Api_Unavailable_And_Token_Provided()
    {
        var userId = Guid.NewGuid();
        var connector = new Mock<IB3Connector>();
        connector.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var importer = new Mock<IB3ImportService>();
        importer
            .Setup(x => x.ConfirmAsync(userId, It.IsAny<ConfirmB3ImportRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new B3ConfirmImportResponse(7));

        var sut = BuildSut(connector: connector, importService: importer);
        await sut.GrantMockConsentAsync(userId, CancellationToken.None);

        var result = await sut.SyncAsync(userId, new B3SyncRequest("replace", "token-123"), CancellationToken.None);

        result.Source.Should().Be("pdf_fallback");
        result.FallbackUsed.Should().BeTrue();
        result.Imported.Should().Be(7);
        importer.Verify(x => x.ConfirmAsync(userId, It.Is<ConfirmB3ImportRequest>(r => r.ImportToken == "token-123" && r.Strategy == "replace"), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static B3SyncService BuildSut(
        Mock<IB3Connector>? connector = null,
        Mock<IB3ImportService>? importService = null)
    {
        return new B3SyncService(
            new MemoryCache(new MemoryCacheOptions()),
            connector?.Object ?? Mock.Of<IB3Connector>(),
            importService?.Object ?? Mock.Of<IB3ImportService>());
    }

    private static B3ImportSnapshot CreateSnapshot() =>
        new(
            "02/2026",
            "Holder",
            "00000000000",
            [new B3ExtractPosition("PETR4", "Acoes", "B3", 10, 30, 300)],
            [],
            []);
}
