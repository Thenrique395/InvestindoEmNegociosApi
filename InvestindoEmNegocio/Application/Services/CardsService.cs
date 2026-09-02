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
    ICurrentSpaceAccessor currentSpaceAccessor,
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
            throw NicknameConflict();

        if (await cardRepository.BrandAndLast4ExistsAsync(userId, request.BrandId, request.Last4, null, cancellationToken))
            throw SameCardConflict(request.Last4);

        var card = new Card(
            userId,
            currentSpaceAccessor.RequireSpaceId(),
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
        catch (DbUpdateException exception)
        {
            throw ConflictFromDatabase(exception, request.Last4);
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
            throw NicknameConflict();

        if (await cardRepository.BrandAndLast4ExistsAsync(userId, request.BrandId, request.Last4, id, cancellationToken))
            throw SameCardConflict(request.Last4);

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
        catch (DbUpdateException exception)
        {
            throw ConflictFromDatabase(exception, request.Last4);
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

        /*
         * Parcela de plano com cartão que esteja sem os campos de fatura tem o ciclo
         * calculado aqui, na leitura.
         *
         * Antes, o filtro exigia os quatro campos e descartava o resto em silêncio: a
         * compra existia, aparecia em Despesas com o cartão certo, e mesmo assim a tela
         * de faturas dizia "nenhuma fatura encontrada". Os campos só são gravados na
         * criação (PlansService.BuildInstallment) e só quando o plano já tem CardId —
         * então quem vinculou o cartão depois, ou cadastrou antes destes campos
         * existirem, ficava invisível para sempre.
         *
         * O cálculo é o mesmo da criação, com o dia de fechamento do cartão. Nada é
         * gravado: é uma projeção de leitura, e a parcela continua como está no banco.
         */
        var cycleByInstallment = new Dictionary<Guid, CardStatementCycle>();
        foreach (var i in installments)
        {
            if (!cardPlans.ContainsKey(i.PlanId)) continue;

            if (i.StatementYear.HasValue &&
                i.StatementMonth.HasValue &&
                i.StatementCloseDate.HasValue &&
                i.StatementDueDate.HasValue)
            {
                cycleByInstallment[i.Id] = new CardStatementCycle(
                    i.StatementYear.Value,
                    i.StatementMonth.Value,
                    i.StatementCloseDate.Value,
                    i.StatementDueDate.Value);
                continue;
            }

            cycleByInstallment[i.Id] = CardStatementCycleCalculator.Calculate(
                i.DueDate,
                card.StatementCloseDay,
                card.DueDay);
        }

        var filteredInstallments = installments
            .Where(i =>
                cycleByInstallment.ContainsKey(i.Id) &&
                (!year.HasValue || cycleByInstallment[i.Id].StatementYear == year.Value) &&
                (!month.HasValue || cycleByInstallment[i.Id].StatementMonth == month.Value))
            .ToList();
        if (filteredInstallments.Count == 0) return [];

        var payments = await paymentRepository.ListByInstallmentIdsAsync(filteredInstallments.Select(i => i.Id), cancellationToken);
        var paidByInstallment = payments
            .GroupBy(p => p.InstallmentId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.PaidAmount));

        var cycles = filteredInstallments
            .GroupBy(i => new
            {
                Year = cycleByInstallment[i.Id].StatementYear,
                Month = cycleByInstallment[i.Id].StatementMonth,
                CloseDate = cycleByInstallment[i.Id].StatementCloseDate,
                DueDate = cycleByInstallment[i.Id].StatementDueDate
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
                        var purchaseDate = cycleByInstallment[i.Id].StatementCloseDate.AddMonths(-1);

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

    private static AppProblemException NicknameConflict() =>
        new("Cartão já existe", "Já existe um cartão com esse nome/apelido.", StatusCodes.Status409Conflict);

    /// <summary>
    /// Recusa do índice único (UserId, BrandId, Last4): é o mesmo cartão, não um
    /// apelido repetido. Dizer "escolha outro apelido" aqui mandava a pessoa
    /// tentar de novo o que nunca ia funcionar.
    /// </summary>
    private static AppProblemException SameCardConflict(string? last4)
    {
        var final = string.IsNullOrWhiteSpace(last4) ? string.Empty : $" terminando em {last4.Trim()}";
        return new AppProblemException(
            "Cartão já cadastrado",
            $"Você já tem um cartão dessa bandeira{final}. Confira a lista de cartões antes de cadastrar outro.",
            StatusCodes.Status409Conflict);
    }

    /// <summary>
    /// Última linha: o insert/update estourou no banco. Traduz a constraint quando dá
    /// para reconhecê-la e, quando não dá, evita afirmar um motivo que pode não ser o real.
    /// </summary>
    private static AppProblemException ConflictFromDatabase(DbUpdateException exception, string? last4)
    {
        var detalhe = $"{exception.InnerException?.Message} {exception.Message}";

        if (detalhe.Contains("Last4", StringComparison.OrdinalIgnoreCase)
            || detalhe.Contains("BrandId", StringComparison.OrdinalIgnoreCase))
        {
            return SameCardConflict(last4);
        }

        if (detalhe.Contains("Nickname", StringComparison.OrdinalIgnoreCase))
        {
            return NicknameConflict();
        }

        return new AppProblemException(
            "Não foi possível salvar o cartão",
            "Os dados enviados conflitam com um cartão já cadastrado. Confira bandeira, número e apelido.",
            StatusCodes.Status409Conflict);
    }

    private static string ResolveNickname(CardRequest request) =>
        string.IsNullOrWhiteSpace(request.Nickname)
            ? request.HolderName?.Trim() ?? string.Empty
            : request.Nickname.Trim();
}
