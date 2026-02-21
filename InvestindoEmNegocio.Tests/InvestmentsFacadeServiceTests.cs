using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class InvestmentsFacadeServiceTests
{
    [Fact]
    public async Task DeletePositionAsync_Should_Throw_404_When_Position_Does_Not_Exist()
    {
        var investmentsService = new Mock<IInvestmentsService>();
        investmentsService
            .Setup(x => x.DeletePositionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var auditService = new Mock<IAuditService>();
        var sut = BuildSut(investmentsService: investmentsService, auditService: auditService);

        Func<Task> act = async () => await sut.DeletePositionAsync(Guid.NewGuid(), Guid.NewGuid(), "127.0.0.1", "xunit");

        var ex = await act.Should().ThrowAsync<AppProblemException>();
        ex.Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        ex.Which.Title.Should().Be("Não encontrado");
        auditService.Verify(x => x.LogAsync(
            It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeletePositionAsync_Should_Log_Audit_When_Successful()
    {
        var userId = Guid.NewGuid();
        var positionId = Guid.NewGuid();
        var investmentsService = new Mock<IInvestmentsService>();
        investmentsService
            .Setup(x => x.DeletePositionAsync(userId, positionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var auditService = new Mock<IAuditService>();
        var sut = BuildSut(investmentsService: investmentsService, auditService: auditService);

        await sut.DeletePositionAsync(userId, positionId, "127.0.0.1", "xunit");

        auditService.Verify(x => x.LogAsync(
            userId,
            "DELETE",
            "InvestmentPosition",
            positionId.ToString(),
            "127.0.0.1",
            "xunit",
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMarketQuoteAsync_Should_Throw_400_When_Symbol_Is_Empty()
    {
        var sut = BuildSut();

        Func<Task> act = async () => await sut.GetMarketQuoteAsync(" ");

        var ex = await act.Should().ThrowAsync<AppProblemException>();
        ex.Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ex.Which.Title.Should().Be("Símbolo obrigatório");
    }

    [Fact]
    public async Task GetMarketQuoteAsync_Should_Throw_503_When_MarketData_Fails()
    {
        var marketDataService = new Mock<IMarketDataService>();
        marketDataService
            .Setup(x => x.GetQuoteAsync("VALE3", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("timeout"));
        var sut = BuildSut(marketDataService: marketDataService);

        Func<Task> act = async () => await sut.GetMarketQuoteAsync("VALE3");

        var ex = await act.Should().ThrowAsync<AppProblemException>();
        ex.Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        ex.Which.Title.Should().Be("Market data indisponível");
    }

    [Fact]
    public async Task AddMovementAsync_Should_Map_ArgumentException_To_400()
    {
        var investmentsService = new Mock<IInvestmentsService>();
        investmentsService
            .Setup(x => x.AddMovementAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CreateInvestmentMovementRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("movimento inválido"));
        var sut = BuildSut(investmentsService: investmentsService);

        Func<Task> act = async () => await sut.AddMovementAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new CreateInvestmentMovementRequest(InvestmentMovementType.COMPRA, 1, 10, DateOnly.FromDateTime(DateTime.UtcNow), null));

        var ex = await act.Should().ThrowAsync<AppProblemException>();
        ex.Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ex.Which.Title.Should().Be("Movimento inválido");
    }

    private static InvestmentsFacadeService BuildSut(
        Mock<IInvestmentsService>? investmentsService = null,
        Mock<IMarketDataService>? marketDataService = null,
        Mock<IAuditService>? auditService = null,
        Mock<IB3ImportService>? b3ImportService = null,
        Mock<IB3SyncService>? b3SyncService = null)
    {
        return new InvestmentsFacadeService(
            investmentsService?.Object ?? Mock.Of<IInvestmentsService>(),
            marketDataService?.Object ?? Mock.Of<IMarketDataService>(),
            auditService?.Object ?? Mock.Of<IAuditService>(),
            b3ImportService?.Object ?? Mock.Of<IB3ImportService>(),
            b3SyncService?.Object ?? Mock.Of<IB3SyncService>(),
            NullLogger<InvestmentsFacadeService>.Instance);
    }
}
