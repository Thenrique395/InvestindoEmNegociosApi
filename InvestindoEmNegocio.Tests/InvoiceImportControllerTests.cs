using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;
using UglyToad.PdfPig.Core;

namespace InvestindoEmNegocio.Tests;

public class InvoiceImportControllerTests
{
    [Fact]
    public async Task Extract_Should_Return_Ok_When_Service_Succeeds()
    {
        var service = new Mock<IInvoiceImportService>();
        service.Setup(x => x.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvoiceExtractResponse("R$ 10,00", null, null, null, null, [], "raw", null, null, null, null, null, null, null, null, null, null, null));
        var controller = new InvoiceImportController(service.Object, NullLogger<InvoiceImportController>.Instance);

        var result = await controller.Extract(new UploadInvoiceRequest { File = BuildFile("application/pdf") }, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Extract_Should_Throw_AppProblem_When_File_Is_Missing_Or_Invalid_ContentType()
    {
        var controller = new InvoiceImportController(Mock.Of<IInvoiceImportService>(), NullLogger<InvoiceImportController>.Instance);

        Func<Task> missing = async () => await controller.Extract(new UploadInvoiceRequest(), CancellationToken.None);
        await missing.Should().ThrowAsync<AppProblemException>();

        Func<Task> invalidType = async () => await controller.Extract(new UploadInvoiceRequest { File = BuildFile("text/plain") }, CancellationToken.None);
        await invalidType.Should().ThrowAsync<AppProblemException>();
    }

    [Fact]
    public async Task Extract_Should_Map_Known_Exceptions_To_AppProblem()
    {
        var service = new Mock<IInvoiceImportService>();
        service.Setup(x => x.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PdfDocumentFormatException("invalid"));
        var controller = new InvoiceImportController(service.Object, NullLogger<InvoiceImportController>.Instance);

        Func<Task> act = async () => await controller.Extract(new UploadInvoiceRequest { File = BuildFile("application/pdf") }, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<AppProblemException>();
        ex.Which.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task Extract_Should_Map_Unexpected_Exception_To_500_AppProblem()
    {
        var service = new Mock<IInvoiceImportService>();
        service.Setup(x => x.ExtractAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var controller = new InvoiceImportController(service.Object, NullLogger<InvoiceImportController>.Instance);

        Func<Task> act = async () => await controller.Extract(new UploadInvoiceRequest { File = BuildFile("application/pdf") }, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<AppProblemException>();
        ex.Which.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task Import_Should_Return_Ok_When_Service_Succeeds()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<IInvoiceImportService>();
        service.Setup(x => x.ImportAsync(userId, It.IsAny<InvoiceImportRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvoiceImportResultResponse(3, 1, 0));

        var controller = new InvoiceImportController(service.Object, NullLogger<InvoiceImportController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                ], "test"))
            }
        };

        var result = await controller.Import(
            new InvoiceImportRequest(null, null, null, true, [new InvoiceImportItemRequest("01/03/2026", "Item", "R$ 10,00")]),
            CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    private static IFormFile BuildFile(string contentType)
    {
        var stream = new MemoryStream([1, 2, 3]);
        return new FormFile(stream, 0, stream.Length, "file", "invoice.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
