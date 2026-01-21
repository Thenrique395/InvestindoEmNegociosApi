using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public class CardsService : ICardsService
{
    private readonly ICardRepository _cardRepository;
    private readonly ICardBrandRepository _brandRepository;
    private readonly IMoneyInstallmentRepository _installmentRepository;

    public CardsService(ICardRepository cardRepository, ICardBrandRepository brandRepository, IMoneyInstallmentRepository installmentRepository)
    {
        _cardRepository = cardRepository;
        _brandRepository = brandRepository;
        _installmentRepository = installmentRepository;
    }

    public async Task<IReadOnlyList<CardResponse>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var data = await _cardRepository.ListByUserAsync(userId, cancellationToken);
        return data.Select(MapToResponse).ToList();
    }

    public async Task<CardResponse> CreateAsync(Guid userId, CardRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _brandRepository.ExistsAsync(request.BrandId, cancellationToken))
            throw new ArgumentException("BrandId não encontrado.");

        var card = new Card(
            userId,
            request.BrandId,
            request.HolderName,
            request.Nickname ?? request.HolderName,
            request.Last4,
            request.Bank,
            request.CreditLimit,
            request.StatementCloseDay,
            request.DueDay);
        await _cardRepository.AddAsync(card, cancellationToken);
        await _cardRepository.SaveChangesAsync(cancellationToken);
        return MapToResponse(card);
    }

    public async Task<CardResponse?> UpdateAsync(Guid userId, Guid id, CardRequest request, CancellationToken cancellationToken = default)
    {
        var card = await _cardRepository.GetByIdAsync(id, userId, cancellationToken);
        if (card is null) return null;

        if (!await _brandRepository.ExistsAsync(request.BrandId, cancellationToken))
            throw new ArgumentException("BrandId não encontrado.");

        card.Update(
            request.BrandId,
            request.HolderName,
            request.Nickname ?? request.HolderName,
            request.Last4,
            request.Bank,
            request.CreditLimit,
            request.StatementCloseDay,
            request.DueDay);
        await _cardRepository.SaveChangesAsync(cancellationToken);
        return MapToResponse(card);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var card = await _cardRepository.GetByIdAsync(id, userId, cancellationToken);
        if (card is null) return false;

        _cardRepository.Remove(card);
        await _cardRepository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<decimal> GetTotalDebtAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _installmentRepository.SumCardDebtAsync(userId, cancellationToken);
    }

    private static CardResponse MapToResponse(Card c) =>
        new(
            c.Id,
            c.BrandId,
            c.HolderName,
            c.Nickname,
            c.Last4,
            c.Bank,
            c.CreditLimit,
            c.StatementCloseDay,
            c.DueDay,
            c.CreatedAt,
            c.UpdatedAt);
}
