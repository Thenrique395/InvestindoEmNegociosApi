using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Finance;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

public class CardsService(
    ICardRepository cardRepository,
    ICardBrandRepository brandRepository,
    IMoneyInstallmentRepository installmentRepository,
    IMoneyPaymentRepository paymentRepository,
    IMoneyPlanRepository planRepository,
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

        var nickname = ResolveNickname(request);
        if (await cardRepository.NicknameExistsAsync(userId, nickname, null, cancellationToken))
            throw new AppProblemException("Cartão já existe", "Já existe um cartão com esse nome/apelido.", StatusCodes.Status409Conflict);

        var card = new Card(
            userId,
            request.BrandId,
            request.HolderName,
            nickname,
            request.Last4,
            request.Bank,
            request.CreditLimit,
            request.StatementCloseDay,
            request.DueDay);
        await cardRepository.AddAsync(card, cancellationToken);

        try
        {
            await cardRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new AppProblemException("Cartão já existe", "Já existe um cartão com esse nome/apelido.", StatusCodes.Status409Conflict);
        }

        _logger.LogInformation("Card created {UserId} {CardId}", userId, card.Id);
        return MapToResponse(card);
    }

    public async Task<CardResponse?> UpdateAsync(Guid userId, Guid id, CardRequest request, CancellationToken cancellationToken = default)
    {
        var card = await cardRepository.GetByIdAsync(id, userId, cancellationToken);
        if (card is null) return null;

        if (!await brandRepository.ExistsAsync(request.BrandId, cancellationToken))
            throw new ArgumentException("BrandId não encontrado.");

        var nickname = ResolveNickname(request);
        if (await cardRepository.NicknameExistsAsync(userId, nickname, id, cancellationToken))
            throw new AppProblemException("Cartão já existe", "Já existe um cartão com esse nome/apelido.", StatusCodes.Status409Conflict);

        card.Update(
            request.BrandId,
            request.HolderName,
            nickname,
            request.Last4,
            request.Bank,
            request.CreditLimit,
            request.StatementCloseDay,
            request.DueDay);

        try
        {
            await cardRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new AppProblemException("Cartão já existe", "Já existe um cartão com esse nome/apelido.", StatusCodes.Status409Conflict);
        }

        _logger.LogInformation("Card updated {UserId} {CardId}", userId, card.Id);
        return MapToResponse(card);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var card = await cardRepository.GetByIdAsync(id, userId, cancellationToken);
        if (card is null) return false;

        card.MarkDeleted(DateTime.UtcNow);
        await cardRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Card deleted {UserId} {CardId}", userId, card.Id);
        return true;
    }

    public Task<decimal> GetTotalDebtAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return installmentRepository.SumCardDebtAsync(userId, cancellationToken);
    }

    public async Task<IReadOnlyList<CardStatementCycleResponse>?> ListStatementCyclesAsync(
        Guid userId,
        Guid cardId,
        int? year = null,
        int? month = null,
        CancellationToken cancellationToken = default)
    {
        var card = await cardRepository.GetByIdAsync(cardId, userId, cancellationToken);
        if (card is null) return null;

        if (year.HasValue && (year.Value < 2000 || year.Value > 2100))
            throw new ArgumentException("Ano inválido para consulta de fatura.");
        if (month.HasValue && (month.Value < 1 || month.Value > 12))
            throw new ArgumentException("Mês inválido para consulta de fatura.");

        var plans = await planRepository.ListByUserAsync(userId, MoneyType.Expense, cancellationToken);
        var cardPlans = plans
            .Where(p => p.CardId == cardId)
            .ToDictionary(p => p.Id, p => p.Title);
        if (cardPlans.Count == 0) return [];

        var installments = await installmentRepository.ListByUserAsync(userId, null, null, null, MoneyType.Expense, cancellationToken);
        var filteredInstallments = installments
            .Where(i =>
                i.StatementYear.HasValue &&
                i.StatementMonth.HasValue &&
                i.StatementCloseDate.HasValue &&
                i.StatementDueDate.HasValue &&
                cardPlans.ContainsKey(i.PlanId) &&
                (!year.HasValue || i.StatementYear == year.Value) &&
                (!month.HasValue || i.StatementMonth == month.Value))
            .ToList();
        if (filteredInstallments.Count == 0) return [];

        var payments = await paymentRepository.ListByInstallmentIdsAsync(filteredInstallments.Select(i => i.Id), cancellationToken);
        var paidByInstallment = payments
            .GroupBy(p => p.InstallmentId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.PaidAmount));

        var cycles = filteredInstallments
            .GroupBy(i => new
            {
                Year = i.StatementYear!.Value,
                Month = i.StatementMonth!.Value,
                CloseDate = i.StatementCloseDate!.Value,
                DueDate = i.StatementDueDate!.Value
            })
            .OrderByDescending(g => g.Key.Year)
            .ThenByDescending(g => g.Key.Month)
            .Select(g =>
            {
                var items = g
                    .OrderBy(i => i.InstallmentNo)
                    .Select(i =>
                    {
                        var paid = paidByInstallment.GetValueOrDefault(i.Id, 0m);
                        var open = CardStatementConsolidationEngine.NormalizeOpenAmount(i.Amount, paid);
                        var purchaseDate = i.StatementCloseDate.HasValue
                            ? i.StatementCloseDate.Value.AddMonths(-1)
                            : i.DueDate;

                        return new CardStatementInstallmentResponse(
                            i.Id,
                            i.PlanId,
                            cardPlans.GetValueOrDefault(i.PlanId, "Despesa"),
                            i.InstallmentNo,
                            purchaseDate,
                            i.DueDate,
                            i.Amount,
                            paid,
                            open,
                            i.Status.ToString());
                    })
                    .ToList();

                return new CardStatementCycleResponse(
                    g.Key.Year,
                    g.Key.Month,
                    g.Key.CloseDate,
                    g.Key.DueDate,
                    items.Sum(x => x.Amount),
                    items.Sum(x => x.PaidAmount),
                    items.Sum(x => x.OpenAmount),
                    items.Count,
                    items);
            })
            .ToList();

        return cycles;
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

    private static string ResolveNickname(CardRequest request) =>
        string.IsNullOrWhiteSpace(request.Nickname)
            ? request.HolderName.Trim()
            : request.Nickname.Trim();
}
