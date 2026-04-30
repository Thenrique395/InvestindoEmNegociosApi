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

public class InvestmentControllersTests
{
    [Fact]
    public async Task GetGoal_Should_Return_NoContent_When_Not_Found()
    {
        var service = new Mock<IInvestmentsService>();
        service.Setup(x => x.GetGoalAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((InvestmentGoalDto?)null);
        var controller = CreateGoalsController(service: service);

        var result = await controller.GetGoal(CancellationToken.None);

        result.Result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task UpsertGoal_Should_Return_Ok()
    {
        var dto = new InvestmentGoalDto(Guid.NewGuid(), 10000m);
        var service = new Mock<IInvestmentsService>();
        service.Setup(x => x.UpsertGoalAsync(It.IsAny<Guid>(), It.IsAny<UpsertInvestmentGoalRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        var controller = CreateGoalsController(service: service);

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
        var controller = CreatePositionsController(service: service);

        var result = await controller.List(new ListQuery(1, 1, "asset", "asc"), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        controller.Response.Headers["X-Total-Count"].ToString().Should().Be("1");
    }

    [Fact]
    public async Task GetPosition_Should_Return_NotFound_When_Missing()
    {
        var service = new Mock<IInvestmentsService>();
        service.Setup(x => x.GetPositionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvestmentPositionDto?)null);
        var controller = CreatePositionsController(service: service);

        var result = await controller.Get(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Create_And_Update_And_Delete_Position_Should_Return_Expected_Status()
    {
        var position = new InvestmentPositionDto(Guid.NewGuid(), InvestmentType.ACOES, "PETR4", 10, 20, DateOnly.FromDateTime(DateTime.UtcNow), "B3", "Acoes", null, []);
        var applicationService = new Mock<IInvestmentsApplicationService>();
        applicationService.Setup(x => x.CreatePositionAsync(It.IsAny<Guid>(), It.IsAny<CreateInvestmentPositionRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(position);
        applicationService.Setup(x => x.UpdatePositionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CreateInvestmentPositionRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(position);
        applicationService.Setup(x => x.DeletePositionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var controller = CreatePositionsController(applicationService: applicationService);

        var create = await controller.Create(new CreateInvestmentPositionRequest(InvestmentType.ACOES, "PETR4", 10, 20, DateOnly.FromDateTime(DateTime.UtcNow), "B3", "Acoes", null), CancellationToken.None);
        var update = await controller.Update(position.Id, new CreateInvestmentPositionRequest(InvestmentType.ACOES, "PETR4", 15, 21, DateOnly.FromDateTime(DateTime.UtcNow), "B3", "Acoes", null), CancellationToken.None);
        var delete = await controller.Delete(position.Id, CancellationToken.None);

        create.Should().BeOfType<OkObjectResult>();
        update.Should().BeOfType<OkObjectResult>();
        delete.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task AddMovement_And_Benchmarks_And_Market_Endpoints_Should_Return_Ok()
    {
        var movement = new InvestmentMovementDto(Guid.NewGuid(), InvestmentMovementType.COMPRA, 1, 10, DateOnly.FromDateTime(DateTime.UtcNow), null);
        var applicationService = new Mock<IInvestmentsApplicationService>();
        applicationService.Setup(x => x.AddMovementAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CreateInvestmentMovementRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(movement);
        applicationService.Setup(x => x.GetMarketQuoteAsync("PETR4", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketQuoteResponse("PETR4", 30m, 1m, "BRL", "Petrobras", DateTimeOffset.UtcNow, "source", false, "provider"));
        applicationService.Setup(x => x.GetMarketProfileAsync("PETR4", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketProfileResponse("PETR4", "Petrobras", null, null, null, null, "source", false, "provider"));
        applicationService.Setup(x => x.GetMarketHistoryAsync("PETR4", "6mo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketHistoryResponse("PETR4", "6mo", "source", false, "provider", []));

        var benchmarks = new Mock<IInvestmentBenchmarksService>();
        benchmarks.Setup(x => x.GetBenchmarksAsync(6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvestmentBenchmarksResponse(6, [new InvestmentBenchmarkItemDto("CDI", 10m, "src", false)]));

        var positionsController = CreatePositionsController(applicationService: applicationService);
        var benchmarksController = new InvestmentBenchmarksController(benchmarks.Object);
        var marketController = new InvestmentMarketController(applicationService.Object);

        (await positionsController.AddMovement(Guid.NewGuid(), new CreateInvestmentMovementRequest(InvestmentMovementType.COMPRA, 1, 10, DateOnly.FromDateTime(DateTime.UtcNow), null), CancellationToken.None))
            .Should().BeOfType<OkObjectResult>();
        (await benchmarksController.Get(6, CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await marketController.GetQuote("PETR4", CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await marketController.GetProfile("PETR4", CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await marketController.GetHistory("PETR4", "6mo", CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task B3_Endpoints_Should_Return_Ok_When_Application_Service_And_Sync_Succeed()
    {
        var applicationService = new Mock<IInvestmentsApplicationService>();
        applicationService.Setup(x => x.ConfirmB3Async(It.IsAny<Guid>(), It.IsAny<ConfirmB3ImportRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new B3ConfirmImportResponse(10));
        applicationService.Setup(x => x.SyncB3Async(It.IsAny<Guid>(), It.IsAny<B3SyncRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new B3SyncResponse("B3", false, 10, "ok"));
        applicationService.Setup(x => x.ExtractB3Async(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new B3ExtractResponse("token", "02/2026", "holder", "doc", new B3ExtractTotals(0, 0, 0), [], [], [], "raw"));

        var sync = new Mock<IB3SyncService>();
        sync.Setup(x => x.GetConsentStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new B3ConsentStatusResponse(true, "B3", DateTime.UtcNow, "ok"));
        sync.Setup(x => x.GrantMockConsentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new B3ConsentStatusResponse(true, "B3", DateTime.UtcNow, "ok"));

        var controller = CreateB3Controller(applicationService: applicationService, b3Sync: sync);
        var file = BuildPdfFormFile();

        (await controller.Extract(new UploadB3ReportRequest { File = file }, CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await controller.Confirm(new ConfirmB3ImportRequest("token"), CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await controller.GetConsent(CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await controller.GrantConsentMock(CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await controller.Sync(new B3SyncRequest(), CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ExtractB3_Should_Throw_AppProblem_When_File_Is_Invalid()
    {
        var controller = CreateB3Controller();

        Func<Task> noFile = async () => await controller.Extract(new UploadB3ReportRequest(), CancellationToken.None);
        await noFile.Should().ThrowAsync<AppProblemException>();

        var txtFile = new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "file", "a.txt") { Headers = new HeaderDictionary(), ContentType = "text/plain" };
        Func<Task> wrongContent = async () => await controller.Extract(new UploadB3ReportRequest { File = txtFile }, CancellationToken.None);
        await wrongContent.Should().ThrowAsync<AppProblemException>();
    }

    [Fact]
    public async Task Any_Action_Should_Throw_Unauthorized_When_User_Is_Missing()
    {
        var controller = new InvestmentGoalsController(
            Mock.Of<IInvestmentsService>(),
            Mock.Of<IInvestmentsService>(),
            Mock.Of<IInvestmentsService>(),
            Mock.Of<IInvestmentsApplicationService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        Func<Task> act = async () => await controller.GetGoal(CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private static InvestmentGoalsController CreateGoalsController(
        Mock<IInvestmentsService>? service = null,
        Mock<IInvestmentsApplicationService>? applicationService = null)
    {
        var aggregate = service?.Object ?? Mock.Of<IInvestmentsService>();
        var controller = new InvestmentGoalsController(
            aggregate,
            aggregate,
            aggregate,
            applicationService?.Object ?? Mock.Of<IInvestmentsApplicationService>());

        SetAuth(controller);
        return controller;
    }

    private static InvestmentPositionsController CreatePositionsController(
        Mock<IInvestmentsService>? service = null,
        Mock<IInvestmentsApplicationService>? applicationService = null)
    {
        var aggregate = service?.Object ?? Mock.Of<IInvestmentsService>();
        var controller = new InvestmentPositionsController(
            aggregate,
            aggregate,
            applicationService?.Object ?? Mock.Of<IInvestmentsApplicationService>());

        SetAuth(controller);
        return controller;
    }

    private static InvestmentB3Controller CreateB3Controller(
        Mock<IInvestmentsApplicationService>? applicationService = null,
        Mock<IB3SyncService>? b3Sync = null)
    {
        var controller = new InvestmentB3Controller(
            applicationService?.Object ?? Mock.Of<IInvestmentsApplicationService>(),
            b3Sync?.Object ?? Mock.Of<IB3SyncService>());

        SetAuth(controller);
        return controller;
    }

    private static void SetAuth(ControllerBase controller)
    {
        var userId = Guid.NewGuid();
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Test");
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        context.Request.Headers["User-Agent"] = "tests";
        context.Request.Headers["X-Forwarded-For"] = "127.0.0.1";

        controller.ControllerContext = new ControllerContext { HttpContext = context };
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
