using System.Security.Claims;
using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Controllers;
using InvestindoEmNegocio.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class InvestmentsControllerTests
{
    [Fact]
    public async Task GetGoal_Should_Return_NoContent_When_Not_Found()
    {
        var service = new Mock<IInvestmentsService>();
        service.Setup(x => x.GetGoalAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((InvestmentGoalDto?)null);
        var controller = CreateController(service: service);

        var result = await controller.GetGoal(CancellationToken.None);

        result.Result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task UpsertGoal_Should_Return_Ok()
    {
        var dto = new InvestmentGoalDto(Guid.NewGuid(), 10000m);
        var service = new Mock<IInvestmentsService>();
        service.Setup(x => x.UpsertGoalAsync(It.IsAny<Guid>(), It.IsAny<UpsertInvestmentGoalRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        var controller = CreateController(service: service);

        var result = await controller.UpsertGoal(new UpsertInvestmentGoalRequest(10000m), CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(dto);
    }

    [Fact]
    public async Task ListPositions_Should_Write_Pagination_Headers_When_Paged()
    {
        var position = new InvestmentPositionDto(Guid.NewGuid(), InvestmentType.ACOES, "PETR4", 10, 20, DateOnly.FromDateTime(DateTime.UtcNow), "B3", "Acoes", null, []);
        var service = new Mock<IInvestmentsService>();
        service.Setup(x => x.ListPositionsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([position]);
        service.Setup(x => x.EnrichWithMarketAsync(It.IsAny<List<InvestmentPositionDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<InvestmentPositionDto> input, CancellationToken _) => input);
        var controller = CreateController(service: service);

        var result = await controller.ListPositions(new ListQuery(1, 1, "asset", "asc"), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        controller.Response.Headers["X-Total-Count"].ToString().Should().Be("1");
    }

    [Fact]
    public async Task GetPosition_Should_Return_NotFound_When_Missing()
    {
        var service = new Mock<IInvestmentsService>();
        service.Setup(x => x.GetPositionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvestmentPositionDto?)null);
        var controller = CreateController(service: service);

        var result = await controller.GetPosition(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Create_And_Update_And_Delete_Position_Should_Return_Expected_Status()
    {
        var position = new InvestmentPositionDto(Guid.NewGuid(), InvestmentType.ACOES, "PETR4", 10, 20, DateOnly.FromDateTime(DateTime.UtcNow), "B3", "Acoes", null, []);
        var facade = new Mock<IInvestmentsFacadeService>();
        facade.Setup(x => x.CreatePositionAsync(It.IsAny<Guid>(), It.IsAny<CreateInvestmentPositionRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(position);
        facade.Setup(x => x.UpdatePositionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CreateInvestmentPositionRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(position);
        facade.Setup(x => x.DeletePositionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var controller = CreateController(facade: facade);

        var create = await controller.CreatePosition(new CreateInvestmentPositionRequest(InvestmentType.ACOES, "PETR4", 10, 20, DateOnly.FromDateTime(DateTime.UtcNow), "B3", "Acoes", null), CancellationToken.None);
        var update = await controller.UpdatePosition(position.Id, new CreateInvestmentPositionRequest(InvestmentType.ACOES, "PETR4", 15, 21, DateOnly.FromDateTime(DateTime.UtcNow), "B3", "Acoes", null), CancellationToken.None);
        var delete = await controller.DeletePosition(position.Id, CancellationToken.None);

        create.Should().BeOfType<OkObjectResult>();
        update.Should().BeOfType<OkObjectResult>();
        delete.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task AddMovement_And_Benchmarks_And_Market_Endpoints_Should_Return_Ok()
    {
        var movement = new InvestmentMovementDto(Guid.NewGuid(), InvestmentMovementType.COMPRA, 1, 10, DateOnly.FromDateTime(DateTime.UtcNow), null);
        var facade = new Mock<IInvestmentsFacadeService>();
        facade.Setup(x => x.AddMovementAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CreateInvestmentMovementRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(movement);
        facade.Setup(x => x.GetMarketQuoteAsync("PETR4", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketQuoteResponse("PETR4", 30m, 1m, "BRL", "Petrobras", DateTimeOffset.UtcNow, "source", false, "provider"));
        facade.Setup(x => x.GetMarketProfileAsync("PETR4", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketProfileResponse("PETR4", "Petrobras", null, null, null, null, "source", false, "provider"));
        facade.Setup(x => x.GetMarketHistoryAsync("PETR4", "6mo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketHistoryResponse("PETR4", "6mo", "source", false, "provider", []));

        var benchmarks = new Mock<IInvestmentBenchmarksService>();
        benchmarks.Setup(x => x.GetBenchmarksAsync(6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvestmentBenchmarksResponse(6, [new InvestmentBenchmarkItemDto("CDI", 10m, "src", false)]));

        var controller = CreateController(facade: facade, benchmarks: benchmarks);

        (await controller.AddMovement(Guid.NewGuid(), new CreateInvestmentMovementRequest(InvestmentMovementType.COMPRA, 1, 10, DateOnly.FromDateTime(DateTime.UtcNow), null), CancellationToken.None))
            .Should().BeOfType<OkObjectResult>();
        (await controller.GetBenchmarks(6, CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await controller.GetMarketQuote("PETR4", CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await controller.GetMarketProfile("PETR4", CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await controller.GetMarketHistory("PETR4", "6mo", CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task B3_Endpoints_Should_Return_Ok_When_Facade_And_Sync_Succeed()
    {
        var facade = new Mock<IInvestmentsFacadeService>();
        facade.Setup(x => x.ConfirmB3Async(It.IsAny<Guid>(), It.IsAny<ConfirmB3ImportRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new B3ConfirmImportResponse(10));
        facade.Setup(x => x.SyncB3Async(It.IsAny<Guid>(), It.IsAny<B3SyncRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new B3SyncResponse("B3", false, 10, "ok"));
        facade.Setup(x => x.ExtractB3Async(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new B3ExtractResponse("token", "02/2026", "holder", "doc", new B3ExtractTotals(0, 0, 0), [], [], [], "raw"));

        var sync = new Mock<IB3SyncService>();
        sync.Setup(x => x.GetConsentStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new B3ConsentStatusResponse(true, "B3", DateTime.UtcNow, "ok"));
        sync.Setup(x => x.GrantMockConsentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new B3ConsentStatusResponse(true, "B3", DateTime.UtcNow, "ok"));

        var controller = CreateController(facade: facade, b3Sync: sync);
        var file = BuildPdfFormFile();

        (await controller.ExtractB3(new UploadB3ReportRequest { File = file }, CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await controller.ConfirmB3(new ConfirmB3ImportRequest("token"), CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await controller.GetB3Consent(CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await controller.GrantB3ConsentMock(CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await controller.SyncB3(new B3SyncRequest(), CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ExtractB3_Should_Throw_AppProblem_When_File_Is_Invalid()
    {
        var controller = CreateController();

        Func<Task> noFile = async () => await controller.ExtractB3(new UploadB3ReportRequest(), CancellationToken.None);
        await noFile.Should().ThrowAsync<AppProblemException>();

        var txtFile = new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "file", "a.txt") { Headers = new HeaderDictionary(), ContentType = "text/plain" };
        Func<Task> wrongContent = async () => await controller.ExtractB3(new UploadB3ReportRequest { File = txtFile }, CancellationToken.None);
        await wrongContent.Should().ThrowAsync<AppProblemException>();
    }

    [Fact]
    public async Task Any_Action_Should_Throw_Unauthorized_When_User_Is_Missing()
    {
        var controller = new InvestmentsController(
            Mock.Of<IInvestmentsService>(),
            Mock.Of<IInvestmentsFacadeService>(),
            Mock.Of<IInvestmentBenchmarksService>(),
            Mock.Of<IB3SyncService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        Func<Task> act = async () => await controller.GetGoal(CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private static InvestmentsController CreateController(
        Mock<IInvestmentsService>? service = null,
        Mock<IInvestmentsFacadeService>? facade = null,
        Mock<IInvestmentBenchmarksService>? benchmarks = null,
        Mock<IB3SyncService>? b3Sync = null)
    {
        var controller = new InvestmentsController(
            service?.Object ?? Mock.Of<IInvestmentsService>(),
            facade?.Object ?? Mock.Of<IInvestmentsFacadeService>(),
            benchmarks?.Object ?? Mock.Of<IInvestmentBenchmarksService>(),
            b3Sync?.Object ?? Mock.Of<IB3SyncService>());

        var userId = Guid.NewGuid();
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Test");
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        context.Request.Headers["User-Agent"] = "tests";
        context.Request.Headers["X-Forwarded-For"] = "127.0.0.1";

        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }

    private static IFormFile BuildPdfFormFile()
    {
        var stream = new MemoryStream([1, 2, 3, 4]);
        return new FormFile(stream, 0, stream.Length, "file", "report.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
    }
}
