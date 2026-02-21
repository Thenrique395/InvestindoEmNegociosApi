using FluentAssertions;
using InvestindoEmNegocio.Application.Services;

namespace InvestindoEmNegocio.Tests;

public class InvoiceImportServiceTests
{
    [Fact]
    public async Task ExtractAsync_Should_Throw_When_Stream_Is_Not_A_Valid_Pdf()
    {
        var sut = new InvoiceImportService(new InvoiceParserFactory());
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("not-a-pdf"));

        Func<Task> act = async () => await sut.ExtractAsync(stream, CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
    }
}
