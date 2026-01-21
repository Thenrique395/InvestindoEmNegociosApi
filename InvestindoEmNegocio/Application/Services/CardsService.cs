using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

public class CardsService(
    ICardRepository cardRepository,
    ICardBrandRepository brandRepository,
    IMoneyInstallmentRepository installmentRepository,
    ILogger<CardsService> logger)
    : ICardsService
{
    private readonly ILogger<CardsService> _logger = logger;
    public async Task<IReadOnlyList<CardResponse>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var data = await cardRepository.ListByUserAsync(userId, cancellationToken);
        return data.Select(MapToResponse).ToList();
    }

    public async Task<CardResponse> CreateAsync(Guid userId, CardRequest request, CancellationToken cancellationToken = default)
    {
        if (!await brandRepository.ExistsAsync(request.BrandId, cancellationToken))
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
        await cardRepository.AddAsync(card, cancellationToken);
        await cardRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Card created {UserId} {CardId}", userId, card.Id);
        return MapToResponse(card);
    }

    public async Task<CardResponse?> UpdateAsync(Guid userId, Guid id, CardRequest request, CancellationToken cancellationToken = default)
    {
        var card = await cardRepository.GetByIdAsync(id, userId, cancellationToken);
        if (card is null) return null;

        if (!await brandRepository.ExistsAsync(request.BrandId, cancellationToken))
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
        await cardRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Card updated {UserId} {CardId}", userId, card.Id);
        return MapToResponse(card);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var card = await cardRepository.GetByIdAsync(id, userId, cancellationToken);
        if (card is null) return false;

        cardRepository.Remove(card);
        await cardRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Card deleted {UserId} {CardId}", userId, card.Id);
        return true;
    }

    public Task<decimal> GetTotalDebtAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return installmentRepository.SumCardDebtAsync(userId, cancellationToken);
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
