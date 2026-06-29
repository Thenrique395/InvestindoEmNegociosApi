using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Services;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Tests;

public class AnthropicClientTests
{
    private const string ApiKey = "test-api-key";

    [Fact]
    public async Task CompleteAsync_Should_Return_Text_From_Response()
    {
        var sut = CreateSut(new FakeHttpMessageHandler((request, _) =>
        {
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri!.AbsolutePath.Should().Be("/v1/messages");
            request.Headers.GetValues("x-api-key").Single().Should().Be(ApiKey);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    content = new[] { new { type = "text", text = "Resposta gerada pela IA." } }
                })
            });
        }));

        var result = await sut.CompleteAsync("system prompt", "pergunta do usuário");

        result.Should().Be("Resposta gerada pela IA.");
    }

    [Fact]
    public async Task CompleteAsync_Should_Throw_503_When_ApiKey_Not_Configured()
    {
        var sut = CreateSut(new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))), apiKey: "");

        var act = async () => await sut.CompleteAsync("system", "question");

        var ex = await act.Should().ThrowAsync<AppProblemException>();
        ex.Which.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task CompleteAsync_Should_Throw_502_When_Upstream_Fails()
    {
        var sut = CreateSut(new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = JsonContent.Create(new { error = "boom" })
            })));

        var act = async () => await sut.CompleteAsync("system", "question");

        var ex = await act.Should().ThrowAsync<AppProblemException>();
        ex.Which.StatusCode.Should().Be(502);
    }

    [Fact]
    public async Task CompleteAsync_Should_Throw_502_When_Response_Has_No_Text_Content()
    {
        var sut = CreateSut(new FakeHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { content = Array.Empty<object>() })
        })));

        var act = async () => await sut.CompleteAsync("system", "question");

        var ex = await act.Should().ThrowAsync<AppProblemException>();
        ex.Which.StatusCode.Should().Be(502);
    }

    private static AnthropicClient CreateSut(HttpMessageHandler handler, string apiKey = ApiKey)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com") };
        var options = Options.Create(new AnthropicOptions { ApiKey = apiKey });
        return new AnthropicClient(httpClient, options);
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            responder(request, cancellationToken);
    }
}
