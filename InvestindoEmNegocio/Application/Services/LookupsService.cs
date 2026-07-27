using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace InvestindoEmNegocio.Application.Services;

// Lookups são dados de REFERÊNCIA estáticos e GLOBAIS (mesmos p/ todos os usuários),
// lidos em toda carga de página. Cache em memória (TTL 10min) tira esses SELECTs do
// caminho mais quente e alivia o pool de conexões do Postgres. Mudanças de admin
// refletem em até 10min.
public class LookupsService(
    IPaymentMethodRepository paymentMethodRepository,
    ICardBrandRepository cardBrandRepository,
    IInstitutionRepository institutionRepository,
    IMemoryCache cache)
    : ILookupPaymentMethodService, ILookupCardBrandService, ILookupInstitutionService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    public async Task<IReadOnlyList<PaymentMethod>> GetPaymentMethodsAsync(CancellationToken cancellationToken = default)
        => (await cache.GetOrCreateAsync("lookup:payment-methods", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            return paymentMethodRepository.ListActiveAsync(cancellationToken);
        }))!;

    public async Task<IReadOnlyList<CardBrand>> GetCardBrandsAsync(CancellationToken cancellationToken = default)
        => (await cache.GetOrCreateAsync("lookup:card-brands", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            return cardBrandRepository.ListActiveAsync(cancellationToken);
        }))!;

    public async Task<IReadOnlyList<Institution>> GetInstitutionsAsync(InstitutionType? type = null, CancellationToken cancellationToken = default)
        => (await cache.GetOrCreateAsync($"lookup:institutions:{type}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            return institutionRepository.ListActiveAsync(type, cancellationToken);
        }))!;
}
