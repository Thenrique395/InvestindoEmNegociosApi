using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace InvestindoEmNegocio.Application.Services;

public sealed class B3SyncService(
    IMemoryCache memoryCache,
    IB3Connector connector,
    IB3ImportService importService) : IB3SyncService
{
    private static readonly TimeSpan ConsentTtl = TimeSpan.FromDays(365);

    public Task<B3ConsentStatusResponse> GetConsentStatusAsync(Guid userId, CancellationToken cancellationToken)
    {
        var consent = memoryCache.Get<B3ConsentState>(ConsentKey(userId));
        if (consent is null)
        {
            return Task.FromResult(new B3ConsentStatusResponse(
                false,
                "B3",
                null,
                "Consentimento B3 não encontrado."));
        }

        return Task.FromResult(new B3ConsentStatusResponse(
            true,
            "B3",
            consent.UpdatedAtUtc,
            "Consentimento ativo."));
    }

    public Task<B3ConsentStatusResponse> GrantMockConsentAsync(Guid userId, CancellationToken cancellationToken)
    {
        var state = new B3ConsentState(DateTime.UtcNow);
        memoryCache.Set(ConsentKey(userId), state, ConsentTtl);

        return Task.FromResult(new B3ConsentStatusResponse(
            true,
            "B3",
            state.UpdatedAtUtc,
            "Consentimento mock registrado para testes."));
    }

    public async Task<B3SyncResponse> SyncAsync(Guid userId, B3SyncRequest request, CancellationToken cancellationToken)
    {
        var consent = memoryCache.Get<B3ConsentState>(ConsentKey(userId));
        if (consent is null)
        {
            return new B3SyncResponse("none", false, 0, "Sem consentimento B3. Autorize antes de sincronizar.");
        }

        var apiAvailable = await connector.IsAvailableAsync(cancellationToken);
        if (apiAvailable)
        {
            var snapshot = await connector.GetLatestSnapshotAsync(userId, cancellationToken);
            if (snapshot is not null)
            {
                var imported = await importService.ImportSnapshotAsync(userId, snapshot, request.Strategy, cancellationToken);
                return new B3SyncResponse("b3_api", false, imported.Imported, "Sincronização concluída via API da B3.");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.FallbackImportToken))
        {
            var imported = await importService.ConfirmAsync(
                userId,
                new ConfirmB3ImportRequest(request.FallbackImportToken, request.Strategy),
                cancellationToken);
            return new B3SyncResponse("pdf_fallback", true, imported.Imported, "API indisponível. Importação realizada via fallback de PDF.");
        }

        return new B3SyncResponse(
            "none",
            false,
            0,
            "API da B3 indisponível e sem fallback de PDF. Extraia um PDF ou configure a integração oficial.");
    }

    private static string ConsentKey(Guid userId) => $"b3:consent:{userId:D}";

    private sealed record B3ConsentState(DateTime UpdatedAtUtc);
}
