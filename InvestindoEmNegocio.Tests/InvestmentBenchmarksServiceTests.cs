using System.Net;
using System.Text;
using FluentAssertions;
using InvestindoEmNegocio.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace InvestindoEmNegocio.Tests;

public class InvestmentBenchmarksServiceTests
{
    [Fact]
    public async Task GetBenchmarksAsync_Should_Return_Zero_When_Bcb_Fails()
    {
        using var httpClient = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)))
        {
            BaseAddress = new Uri("https://fake-bcb/")
        };

        var sut = new InvestmentBenchmarksService(httpClient, NullLogger<InvestmentBenchmarksService>.Instance);

        var result = await sut.GetBenchmarksAsync(12, CancellationToken.None);

        result.Months.Should().Be(12);
        result.Items.Should().Contain(i => i.Name == "SELIC (BCB)" && i.ReturnPercent == 0);
        result.Items.Should().Contain(i => i.Name == "IPCA (BCB)" && i.ReturnPercent == 0);
    }

    [Fact]
    public async Task GetBenchmarksAsync_Should_Parse_And_Accumulate_Bcb_Series()
    {
        using var httpClient = new HttpClient(new StubHandler(request =>
        {
            var isSelic = request.RequestUri!.ToString().Contains("sgs.11", StringComparison.OrdinalIgnoreCase);
            var body = isSelic
                ? "[{\"data\":\"01/01/2026\",\"valor\":\"1,00\"},{\"data\":\"01/02/2026\",\"valor\":\"1,00\"}]"
                : "[{\"data\":\"01/01/2026\",\"valor\":\"0,50\"}]";

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }))
        {
            BaseAddress = new Uri("https://fake-bcb/")
        };

        var sut = new InvestmentBenchmarksService(httpClient, NullLogger<InvestmentBenchmarksService>.Instance);

        var result = await sut.GetBenchmarksAsync(12, CancellationToken.None);

        var selic = result.Items.Single(i => i.Name == "SELIC (BCB)").ReturnPercent;
        var ipca = result.Items.Single(i => i.Name == "IPCA (BCB)").ReturnPercent;

        selic.Should().BeApproximately(2.01m, 0.01m);
        ipca.Should().BeApproximately(0.50m, 0.01m);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
