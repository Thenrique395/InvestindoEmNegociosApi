using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace InvestindoEmNegocio.Application.Services;

public class AccountAnalyticsService(
    IAccountRepository accountRepository,
    IAccountTransactionRepository accountTransactionRepository,
    IMoneyInstallmentRepository moneyInstallmentRepository,
    IMoneyPaymentRepository moneyPaymentRepository,
    IMoneyPlanRepository moneyPlanRepository,
    ICardRepository cardRepository,
    ICategoryRepository categoryRepository,
    ILoanContractRepository loanContractRepository,
    ILoanInstallmentRepository loanInstallmentRepository,
    IInvestmentPositionQueryService investmentPositionQueryService,
    IInvestmentMarketEnrichmentService investmentMarketEnrichmentService,
    ICashflowProjectionEngine cashflowProjectionEngine,
    IRiskBotService riskBotService,
    IInsightEngineService insightEngineService,
    IRecommendationEngineService recommendationEngineService,
    IMemoryCache cache) : IAccountAnalyticsService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private static string CacheKey(Guid userId, string method, string? period = null, DateOnly? date = null, int? months = null)
    {
        var key = $"analytics:{userId}:{method}";
        if (period is not null) key += $":{period}";
        if (date is not null) key += $":{date:yyyy-MM-dd}";
        if (months is not null) key += $":{months}";
        return key;
    }
    public async Task<RealAvailableBalanceResponse> GetRealAvailableBalanceAsync(
        Guid userId,
        string period = "month",
        DateOnly? referenceDate = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPeriod = NormalizeAnalyticsPeriod(period);
        var cacheKey = CacheKey(userId, "real-balance", normalizedPeriod, referenceDate);
        if (cache.TryGetValue(cacheKey, out RealAvailableBalanceResponse? cached) && cached is not null)
            return cached;
        var anchorDate = referenceDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var (periodStart, periodEnd) = ResolvePeriodRange(anchorDate, normalizedPeriod);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dueSoonLimit = today.AddDays(5);

        var accounts = await accountRepository.ListByUserAsync(userId, cancellationToken);
        decimal activeAccountsBalance = 0m;

        foreach (var account in accounts.Where(a => a.IsActive))
        {
            var net = await accountTransactionRepository.SumSignedAmountByAccountAsync(account.Id, userId, cancellationToken);
            activeAccountsBalance += account.InitialBalance + net;
        }

        var pendingExpenses = await moneyInstallmentRepository.ListByUserAsync(
            userId,
            null,
            periodStart,
            periodEnd,
            Domain.Enums.MoneyType.Expense,
            cancellationToken);
        var pendingIncomes = await moneyInstallmentRepository.ListByUserAsync(
            userId,
            InstallmentStatus.Open,
            periodStart,
            periodEnd,
            Domain.Enums.MoneyType.Income,
            cancellationToken);
        var pendingLoanInstallments = await loanInstallmentRepository.ListByUserAsync(userId, cancellationToken) ?? [];

        var openExpenseItems = pendingExpenses
            .Where(i => i.Status == InstallmentStatus.Open || i.Status == InstallmentStatus.PartiallyPaid)
            .ToList();
        var openLoanItems = pendingLoanInstallments
            .Where(i => i.Status == LoanInstallmentStatus.Open && i.DueDate >= periodStart && i.DueDate <= periodEnd)
            .ToList();

        var pendingExpensesAmount = openExpenseItems.Sum(i => i.Amount) + openLoanItems.Sum(i => i.TotalAmount);
        var pendingIncomesAmount = pendingIncomes.Sum(i => i.Amount);
        var overdueExpensesAmount = openExpenseItems.Where(i => i.DueDate < today).Sum(i => i.Amount)
            + openLoanItems.Where(i => i.DueDate < today).Sum(i => i.TotalAmount);
        var overdueExpensesCount = openExpenseItems.Count(i => i.DueDate < today)
            + openLoanItems.Count(i => i.DueDate < today);
        var dueSoonExpensesAmount = openExpenseItems
            .Where(i => i.DueDate >= today && i.DueDate <= dueSoonLimit)
            .Sum(i => i.Amount)
            + openLoanItems.Where(i => i.DueDate >= today && i.DueDate <= dueSoonLimit).Sum(i => i.TotalAmount);

        var result = new RealAvailableBalanceResponse(
            normalizedPeriod,
            anchorDate,
            periodStart,
            periodEnd,
            activeAccountsBalance,
            pendingExpensesAmount,
            openExpenseItems.Count + openLoanItems.Count,
            pendingIncomesAmount,
            pendingIncomes.Count,
            activeAccountsBalance - pendingExpensesAmount,
            activeAccountsBalance - pendingExpensesAmount + pendingIncomesAmount,
            overdueExpensesAmount,
            overdueExpensesCount,
            dueSoonExpensesAmount);
        cache.Set(cacheKey, result, CacheTtl);
        return result;
    }

    public async Task<DebtSummaryResponse> GetDebtSummaryAsync(
        Guid userId,
        DateOnly? referenceDate = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKey(userId, "debts", date: referenceDate);
        if (cache.TryGetValue(cacheKey, out DebtSummaryResponse? cached) && cached is not null)
            return cached;

        var anchorDate = referenceDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dueSoonLimit = today.AddDays(7);
        var items = await BuildOpenDebtItemsAsync(userId, cancellationToken);

        var buckets = new List<DebtSummaryBucketResponse>
        {
            new("cards", "Cartões", items.Where(i => i.Family == "card").Sum(i => i.OpenAmount), items.Count(i => i.Family == "card")),
            new("other", "Outras obrigações", items.Where(i => i.Family == "liability").Sum(i => i.OpenAmount), items.Count(i => i.Family == "liability")),
            new("overdue", "Em atraso", items.Where(i => i.DueDate < today).Sum(i => i.OpenAmount), items.Count(i => i.DueDate < today))
        };

        var result = new DebtSummaryResponse(
            anchorDate,
            items.Sum(i => i.OpenAmount),
            items.Where(i => i.Family == "card").Sum(i => i.OpenAmount),
            items.Where(i => i.Family == "liability").Sum(i => i.OpenAmount),
            items.Where(i => i.DueDate < today).Sum(i => i.OpenAmount),
            items.Where(i => i.DueDate >= today && i.DueDate <= dueSoonLimit).Sum(i => i.OpenAmount),
            items.Count,
            buckets,
            items
                .OrderBy(i => i.DueDate)
                .ThenByDescending(i => i.OpenAmount)
                .Take(6)
                .ToList());
        cache.Set(cacheKey, result, CacheTtl);
        return result;
    }

    public async Task<SubscriptionsSummaryResponse> GetSubscriptionsSummaryAsync(
        Guid userId,
        DateOnly? referenceDate = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKey(userId, "subscriptions", date: referenceDate);
        if (cache.TryGetValue(cacheKey, out SubscriptionsSummaryResponse? cached) && cached is not null)
            return cached;

        var anchorDate = referenceDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var categories = await categoryRepository.ListForUserAsync(userId, MoneyType.Expense, cancellationToken);
        var assinaturasCategoryId = categories.FirstOrDefault(c => c.Name.Equals("Assinaturas", StringComparison.OrdinalIgnoreCase))?.Id;

        IReadOnlyList<SubscriptionSummaryItemResponse> items = [];
        if (assinaturasCategoryId.HasValue)
        {
            var plans = await moneyPlanRepository.ListByUserAsync(userId, MoneyType.Expense, cancellationToken);
            var subscriptionPlans = plans
                .Where(p => p.Status == PlanStatus.Active
                    && p.Schedule == ScheduleType.Recurring
                    && p.CategoryId == assinaturasCategoryId.Value)
                .ToList();

            if (subscriptionPlans.Count > 0)
            {
                var cards = await cardRepository.ListByUserAsync(userId, cancellationToken);
                var cardNameById = cards.ToDictionary(c => c.Id, c => c.Nickname);

                items = subscriptionPlans
                    .OrderByDescending(p => p.Amount)
                    .Select(p => new SubscriptionSummaryItemResponse(
                        p.Id,
                        p.Title,
                        p.Amount,
                        p.CardId.HasValue && cardNameById.TryGetValue(p.CardId.Value, out var cardName) ? cardName : null))
                    .ToList();
            }
        }

        var result = new SubscriptionsSummaryResponse(anchorDate, items.Sum(i => i.Amount), items.Count, items);
        cache.Set(cacheKey, result, CacheTtl);
        return result;
    }

    public async Task<NetWorthSummaryResponse> GetNetWorthSummaryAsync(
        Guid userId,
        DateOnly? referenceDate = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKey(userId, "net-worth", date: referenceDate);
        if (cache.TryGetValue(cacheKey, out NetWorthSummaryResponse? cached) && cached is not null)
            return cached;

        var anchorDate = referenceDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var accounts = await accountRepository.ListByUserAsync(userId, cancellationToken);
        decimal accountsBalance = 0m;

        foreach (var account in accounts.Where(a => a.IsActive))
        {
            var net = await accountTransactionRepository.SumSignedAmountByAccountAsync(account.Id, userId, cancellationToken);
            accountsBalance += account.InitialBalance + net;
        }

        var positions = await investmentPositionQueryService.ListPositionsAsync(userId, cancellationToken);
        var enrichedPositions = await investmentMarketEnrichmentService.EnrichWithMarketAsync(positions, cancellationToken);
        var activePositions = enrichedPositions.Where(p => p.Quantity > 0).ToList();
        var investmentsBalance = activePositions.Where(IsFinancialInvestment).Sum(CalculatePositionValue);
        var tangibleAssetsBalance = activePositions.Where(IsTangibleAsset).Sum(CalculatePositionValue);

        var openLiabilities = await BuildOpenDebtItemsAsync(userId, cancellationToken);
        var cardDebt = openLiabilities.Where(i => i.Family == "card").Sum(i => i.OpenAmount);
        var totalLiabilities = openLiabilities.Sum(i => i.OpenAmount);
        var otherOpenLiabilities = Math.Max(totalLiabilities - cardDebt, 0m);
        var totalAssets = accountsBalance + investmentsBalance + tangibleAssetsBalance;

        var result = new NetWorthSummaryResponse(
            anchorDate,
            new WealthAssetBreakdownResponse(accountsBalance, investmentsBalance, tangibleAssetsBalance, totalAssets),
            new WealthLiabilityBreakdownResponse(cardDebt, otherOpenLiabilities, totalLiabilities),
            totalAssets - totalLiabilities,
            activePositions.Count,
            openLiabilities.Count,
            anchorDate.ToString("MM/yyyy"));
        cache.Set(cacheKey, result, CacheTtl);
        return result;
    }

    public async Task<NetWorthHistoryResponse> GetNetWorthHistoryAsync(
        Guid userId,
        int months = 12,
        DateOnly? referenceDate = null,
        CancellationToken cancellationToken = default)
    {
        if (months is < 3 or > 24)
            throw new ArgumentException("Quantidade de meses inválida. Use um valor entre 3 e 24.");

        var cacheKey = CacheKey(userId, "net-worth-history", date: referenceDate, months: months);
        if (cache.TryGetValue(cacheKey, out NetWorthHistoryResponse? cached) && cached is not null)
            return cached;

        var anchorDate = referenceDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var accounts = await accountRepository.ListByUserAsync(userId, cancellationToken);
        var accountTransactions = new Dictionary<Guid, List<AccountTransaction>>();

        foreach (var account in accounts)
        {
            var items = await accountTransactionRepository.ListByAccountAsync(
                account.Id,
                userId,
                null,
                ToMonthEndUtc(anchorDate),
                cancellationToken);
            accountTransactions[account.Id] = items;
        }

        var positions = await investmentPositionQueryService.ListPositionsAsync(userId, cancellationToken);
        var enrichedPositions = await investmentMarketEnrichmentService.EnrichWithMarketAsync(positions, cancellationToken);

        var installments = await moneyInstallmentRepository.ListByUserAsync(
            userId,
            null,
            null,
            null,
            Domain.Enums.MoneyType.Expense,
            cancellationToken);
        var payments = await moneyPaymentRepository.ListByInstallmentIdsAsync(installments.Select(i => i.Id), cancellationToken);
        var paymentsByInstallment = payments
            .GroupBy(p => p.InstallmentId)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.PaidAt).ToList());

        var timeline = BuildMonthTimeline(anchorDate, months);
        var points = new List<NetWorthHistoryPointResponse>(timeline.Count);
        var notes = new List<string>();
        var hasEstimatedPoints = false;

        foreach (var month in timeline)
        {
            var monthEndUtc = ToMonthEndUtc(month);
            var accountsBalance = accounts.Sum(account =>
            {
                if (account.CreatedAt > monthEndUtc) return 0m;
                var tx = accountTransactions.GetValueOrDefault(account.Id) ?? [];
                var signed = tx
                    .Where(t => t.OccurredAt <= monthEndUtc)
                    .Sum(t => t.Kind == AccountTransactionKind.Credit ? t.Amount : -t.Amount);
                return account.InitialBalance + signed;
            });

            var pointEstimated = false;
            var investmentsBalance = enrichedPositions.Where(IsFinancialInvestment).Sum(position =>
            {
                var value = CalculateInvestmentValueAt(position, month, out var estimated);
                pointEstimated |= estimated;
                return value;
            });
            var tangibleAssetsBalance = enrichedPositions.Where(IsTangibleAsset).Sum(position =>
            {
                var value = CalculateInvestmentValueAt(position, month, out var estimated);
                pointEstimated |= estimated;
                return value;
            });

            var liabilities = installments.Sum(installment =>
            {
                var createdAt = DateOnly.FromDateTime(installment.CreatedAt);
                if (createdAt > month) return 0m;

                var paidUntilMonthEnd = paymentsByInstallment.GetValueOrDefault(installment.Id)?.Where(p => p.PaidAt <= monthEndUtc).Sum(p => p.PaidAmount) ?? 0m;
                return Math.Max(installment.Amount - paidUntilMonthEnd, 0m);
            });

            var totalAssets = accountsBalance + investmentsBalance + tangibleAssetsBalance;
            var netWorth = totalAssets - liabilities;
            hasEstimatedPoints |= pointEstimated;

            points.Add(new NetWorthHistoryPointResponse(
                month,
                month.ToString("MM/yy"),
                accountsBalance,
                investmentsBalance,
                tangibleAssetsBalance,
                totalAssets,
                liabilities,
                netWorth,
                pointEstimated));
        }

        if (hasEstimatedPoints)
        {
            notes.Add("Série patrimonial estimada a partir de movimentos de investimento e preços atuais quando não há marcação histórica mensal.");
        }
        if (enrichedPositions.Any(IsTangibleAsset))
        {
            notes.Add("Imóveis e veículos entram como ativos patrimoniais manuais, valorizados pelo saldo atual informado no cadastro.");
        }

        var result = new NetWorthHistoryResponse(
            anchorDate,
            months,
            hasEstimatedPoints,
            notes,
            points);
        cache.Set(cacheKey, result, CacheTtl);
        return result;
    }

    public async Task<CashflowProjectionResponse> GetProjectionAsync(
        Guid userId,
        string period = "month",
        DateOnly? referenceDate = null,
        CancellationToken cancellationToken = default)
    {
        var key = CacheKey(userId, "projection", period, referenceDate);
        if (cache.TryGetValue(key, out CashflowProjectionResponse? c1) && c1 is not null) return c1;
        var result = await cashflowProjectionEngine.ProjectAsync(userId, period, referenceDate, cancellationToken);
        cache.Set(key, result, CacheTtl);
        return result;
    }

    public async Task<RiskBotAssessmentResponse> GetRiskAssessmentAsync(
        Guid userId,
        string period = "month",
        DateOnly? referenceDate = null,
        CancellationToken cancellationToken = default)
    {
        var key = CacheKey(userId, "risk", period, referenceDate);
        if (cache.TryGetValue(key, out RiskBotAssessmentResponse? c2) && c2 is not null) return c2;
        var result = await riskBotService.AssessAsync(userId, period, referenceDate, cancellationToken);
        cache.Set(key, result, CacheTtl);
        return result;
    }

    public async Task<InsightEngineResponse> GetInsightsAsync(
        Guid userId,
        string period = "month",
        DateOnly? referenceDate = null,
        CancellationToken cancellationToken = default)
    {
        var key = CacheKey(userId, "insights", period, referenceDate);
        if (cache.TryGetValue(key, out InsightEngineResponse? c3) && c3 is not null) return c3;
        var result = await insightEngineService.BuildAsync(userId, period, referenceDate, cancellationToken);
        cache.Set(key, result, CacheTtl);
        return result;
    }

    public async Task<RecommendationEngineResponse> GetRecommendationsAsync(
        Guid userId,
        string period = "month",
        DateOnly? referenceDate = null,
        CancellationToken cancellationToken = default)
    {
        var key = CacheKey(userId, "recommendations", period, referenceDate);
        if (cache.TryGetValue(key, out RecommendationEngineResponse? c4) && c4 is not null) return c4;
        var result = await recommendationEngineService.BuildAsync(userId, period, referenceDate, cancellationToken);
        cache.Set(key, result, CacheTtl);
        return result;
    }

    private static string NormalizeAnalyticsPeriod(string? period)
    {
        if (string.IsNullOrWhiteSpace(period)) return "month";

        return period.Trim().ToLowerInvariant() switch
        {
            "month" => "month",
            "quarter" => "quarter",
            "year" => "year",
            _ => throw new ArgumentException("Período inválido. Use month, quarter ou year.")
        };
    }

    private static (DateOnly Start, DateOnly End) ResolvePeriodRange(DateOnly anchorDate, string period)
    {
        return period switch
        {
            "year" => (new DateOnly(anchorDate.Year, 1, 1), new DateOnly(anchorDate.Year, 12, 31)),
            "quarter" => ResolveQuarterRange(anchorDate),
            _ => (new DateOnly(anchorDate.Year, anchorDate.Month, 1), new DateOnly(anchorDate.Year, anchorDate.Month, DateTime.DaysInMonth(anchorDate.Year, anchorDate.Month)))
        };
    }

    private static (DateOnly Start, DateOnly End) ResolveQuarterRange(DateOnly anchorDate)
    {
        var quarterIndex = (anchorDate.Month - 1) / 3;
        var startMonth = quarterIndex * 3 + 1;
        var endMonth = startMonth + 2;
        return (
            new DateOnly(anchorDate.Year, startMonth, 1),
            new DateOnly(anchorDate.Year, endMonth, DateTime.DaysInMonth(anchorDate.Year, endMonth)));
    }

    private static bool IsFinancialInvestment(InvestmentPositionDto position) =>
        position.Type is InvestmentType.RF or InvestmentType.ACOES or InvestmentType.FUNDOS or InvestmentType.CRIPTO;

    private static bool IsTangibleAsset(InvestmentPositionDto position) =>
        position.Type is InvestmentType.IMOVEL or InvestmentType.VEICULO;

    private static decimal CalculatePositionValue(InvestmentPositionDto position)
    {
        var unitPrice = position.MarketPrice.GetValueOrDefault() > 0m
            ? position.MarketPrice!.Value
            : position.AvgPrice;
        return position.Quantity * unitPrice;
    }

    private static decimal CalculateInvestmentValueAt(InvestmentPositionDto position, DateOnly monthEnd, out bool isEstimated)
    {
        isEstimated = false;
        if (position.OpenedAt > monthEnd) return 0m;

        var moves = (position.Movements ?? [])
            .Where(m => m.Date <= monthEnd)
            .OrderBy(m => m.Date)
            .ToList();

        if (moves.Count == 0)
        {
            isEstimated = true;
            return CalculatePositionValue(position);
        }

        decimal quantity = 0m;
        decimal avgPrice = 0m;
        decimal? lastObservedPrice = null;

        foreach (var move in moves)
        {
            var value = move.Quantity * move.Price;
            switch (move.Type)
            {
                case InvestmentMovementType.COMPRA:
                case InvestmentMovementType.APORTE:
                    var totalAtual = quantity * avgPrice;
                    quantity += move.Quantity;
                    avgPrice = quantity > 0 ? (totalAtual + value) / quantity : 0m;
                    lastObservedPrice = move.Price;
                    break;
                case InvestmentMovementType.VENDA:
                case InvestmentMovementType.RESGATE:
                    quantity = Math.Max(quantity - move.Quantity, 0m);
                    lastObservedPrice = move.Price;
                    break;
                default:
                    break;
            }
        }

        if (quantity <= 0m) return 0m;

        isEstimated = true;
        var unitPrice = lastObservedPrice.GetValueOrDefault() > 0m
            ? lastObservedPrice!.Value
            : position.MarketPrice.GetValueOrDefault() > 0m
                ? position.MarketPrice!.Value
                : avgPrice > 0m
                    ? avgPrice
                    : position.AvgPrice;
        return quantity * unitPrice;
    }

    private static List<DateOnly> BuildMonthTimeline(DateOnly anchorDate, int months)
    {
        var timeline = new List<DateOnly>(months);
        for (var i = months - 1; i >= 0; i--)
        {
            var month = anchorDate.AddMonths(-i);
            timeline.Add(new DateOnly(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month)));
        }
        return timeline;
    }

    private static DateTime ToMonthEndUtc(DateOnly value)
    {
        return value.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc);
    }

    private async Task<List<DebtSummaryItemResponse>> BuildOpenDebtItemsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var installments = await moneyInstallmentRepository.ListByUserAsync(
            userId,
            null,
            null,
            null,
            Domain.Enums.MoneyType.Expense,
            cancellationToken);
        var openInstallments = installments
            .Where(i => i.Status is InstallmentStatus.Open or InstallmentStatus.PartiallyPaid)
            .ToList();
        if (openInstallments.Count == 0) return [];

        var plans = await moneyPlanRepository.ListByUserAsync(userId, Domain.Enums.MoneyType.Expense, cancellationToken);
        var cards = await cardRepository.ListByUserAsync(userId, cancellationToken);
        var loanContracts = await loanContractRepository.ListByUserAsync(userId, cancellationToken) ?? [];
        var loanInstallments = await loanInstallmentRepository.ListByUserAsync(userId, cancellationToken) ?? [];
        var payments = await moneyPaymentRepository.ListByInstallmentIdsAsync(openInstallments.Select(i => i.Id), cancellationToken);

        var planLookup = plans.ToDictionary(p => p.Id, p => p);
        var cardLookup = cards.ToDictionary(c => c.Id, c => string.IsNullOrWhiteSpace(c.Nickname) ? c.HolderName : c.Nickname);
        var paidLookup = payments
            .GroupBy(p => p.InstallmentId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.PaidAmount));

        var debtItems = openInstallments
            .Select(installment =>
            {
                var plan = planLookup.GetValueOrDefault(installment.PlanId);
                var paidAmount = paidLookup.GetValueOrDefault(installment.Id, 0m);
                var openAmount = Math.Max(installment.Amount - paidAmount, 0m);
                if (openAmount <= 0m) return null;

                var family = plan?.CardId is not null ? "card" : "liability";
                var relatedName = plan?.CardId is not null && cardLookup.TryGetValue(plan.CardId.Value, out var cardName)
                    ? cardName
                    : null;
                var dueDate = installment.StatementDueDate ?? installment.DueDate;
                var statementReference = installment.StatementMonth.HasValue && installment.StatementYear.HasValue
                    ? $"{installment.StatementMonth:00}/{installment.StatementYear:0000}"
                    : null;

                return new DebtSummaryItemResponse(
                    installment.Id,
                    installment.PlanId,
                    family,
                    plan?.Title ?? "Despesa",
                    relatedName,
                    dueDate,
                    installment.Amount,
                    paidAmount,
                    openAmount,
                    installment.Status.ToString(),
                    statementReference);
            })
            .Where(i => i is not null)
            .Cast<DebtSummaryItemResponse>()
            .ToList();

        debtItems.AddRange(loanInstallments
            .Where(x => x.Status == LoanInstallmentStatus.Open)
            .Select(x =>
            {
                var contract = loanContracts.FirstOrDefault(c => c.Id == x.ContractId);
                return new DebtSummaryItemResponse(
                    x.Id,
                    x.ContractId,
                    "liability",
                    contract?.Title ?? "Empréstimo",
                    "Contrato de empréstimo",
                    x.DueDate,
                    x.TotalAmount,
                    0m,
                    x.TotalAmount,
                    x.Status.ToString(),
                    null);
            }));

        return debtItems;
    }
}
