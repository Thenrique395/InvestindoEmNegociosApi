using System.Net.Http.Json;
using System.Text.Json.Serialization;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Application.Services;

/// <summary>
/// Chamada HTTP direta à Messages API da Anthropic — sem SDK oficial, mesmo princípio já usado
/// pra <see cref="MercadoPagoBillingGateway"/>: vocabulário do provedor isolado nesta classe,
/// resto da aplicação só vê <see cref="IAnthropicClient.CompleteAsync"/>.
/// </summary>
public sealed class AnthropicClient(HttpClient httpClient, IOptions<AnthropicOptions> options) : IAnthropicClient
{
    private readonly AnthropicOptions _options = options.Value;

    public async Task<string> CompleteAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken = default)
    {
        EnsureApiKeyConfigured();

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/messages")
        {
            Content = JsonContent.Create(new MessagesRequest
            {
                Model = _options.Model,
                MaxTokens = _options.MaxTokens,
                System = systemPrompt,
                Messages = [new MessageRequest { Role = "user", Content = userMessage }]
            })
        };
        request.Headers.Add("x-api-key", _options.ApiKey);
        request.Headers.Add("anthropic-version", _options.ApiVersion);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<MessagesResponse>(cancellationToken: cancellationToken)
            ?? throw new AppProblemException("Assistente indisponível", "Resposta vazia da Anthropic.", StatusCodes.Status502BadGateway);

        var text = body.Content?.FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(text))
            throw new AppProblemException("Assistente indisponível", "Resposta sem conteúdo de texto da Anthropic.", StatusCodes.Status502BadGateway);

        return text;
    }

    private void EnsureApiKeyConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new AppProblemException("Assistente indisponível", "A API da Anthropic não está configurada neste ambiente.", StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new AppProblemException(
            "Assistente indisponível",
            $"Anthropic retornou {(int)response.StatusCode}: {detail}",
            StatusCodes.Status502BadGateway);
    }

    private sealed class MessagesRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
        [JsonPropertyName("max_tokens")] public int MaxTokens { get; set; }
        [JsonPropertyName("system")] public string System { get; set; } = string.Empty;
        [JsonPropertyName("messages")] public MessageRequest[] Messages { get; set; } = [];
    }

    private sealed class MessageRequest
    {
        [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
        [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
    }

    private sealed class MessagesResponse
    {
        [JsonPropertyName("content")] public List<MessagesResponseContent>? Content { get; set; }
    }

    private sealed class MessagesResponseContent
    {
        [JsonPropertyName("text")] public string? Text { get; set; }
    }
}
