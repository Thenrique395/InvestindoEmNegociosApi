using System.Net.Http.Headers;
using System.Text.Json;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace InvestindoEmNegocio.Application.Services;

public sealed class B3ApiClient(
    HttpClient httpClient,
    IOptions<B3ApiOptions> options,
    ILogger<B3ApiClient> logger) : IB3Connector
{
    private readonly B3ApiOptions _options = options.Value;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        var enabled = _options.Enabled &&
                      !string.IsNullOrWhiteSpace(_options.BaseUrl) &&
                      !string.IsNullOrWhiteSpace(_options.ClientId) &&
                      !string.IsNullOrWhiteSpace(_options.ClientSecret);
        return Task.FromResult(enabled);
    }

    public async Task<B3ImportSnapshot?> GetLatestSnapshotAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!await IsAvailableAsync(cancellationToken))
        {
            return null;
        }

        // Fluxo preparado para integração oficial da B3.
        // Endpoint e autenticação finais dependem da contratação/licenciamento no ambiente do cliente.
        var request = new HttpRequestMessage(HttpMethod.Get, "/investor/snapshot/latest");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-Client-Id", _options.ClientId);
        request.Headers.Add("X-Client-Secret", _options.ClientSecret);
        request.Headers.Add("X-User-Id", userId.ToString("D"));

        try
        {
            var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("B3 API retornou status {StatusCode}", (int)response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<B3ImportSnapshot>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao consultar snapshot na B3 API.");
            return null;
        }
    }
}
