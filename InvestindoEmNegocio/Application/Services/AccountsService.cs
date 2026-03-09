using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

public class AccountsService(
    IAccountRepository accountRepository,
    IAccountTransactionRepository accountTransactionRepository,
    IMoneyInstallmentRepository moneyInstallmentRepository,
    IMoneyPaymentRepository moneyPaymentRepository,
    IMoneyPlanRepository moneyPlanRepository,
    ICardRepository cardRepository,
    IInvestmentsService investmentsService,
    ICashflowProjectionEngine cashflowProjectionEngine,
    IRiskBotService riskBotService,
    IInsightEngineService insightEngineService,
    IRecommendationEngineService recommendationEngineService,
    ILogger<AccountsService> logger) : IAccountsService
{
    private readonly ILogger<AccountsService> _logger = logger;

    public async Task<IReadOnlyList<AccountResponse>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var accounts = await accountRepository.ListByUserAsync(userId, cancellationToken);
        var responses = new List<AccountResponse>(accounts.Count);

        foreach (var account in accounts)
        {
            var net = await accountTransactionRepository.SumSignedAmountByAccountAsync(account.Id, userId, cancellationToken);
            responses.Add(MapToResponse(account, net));
        }

        return responses;
    }

    public async Task<AccountResponse> CreateAsync(Guid userId, AccountRequest request, CancellationToken cancellationToken = default)
    {
        if (await accountRepository.ExistsByNameAsync(userId, request.Name, null, cancellationToken))
            throw new ArgumentException("Já existe uma conta com esse nome.");

        var account = new Account(userId, request.Name, request.Type, request.InitialBalance);
        if (!request.IsActive) account.Deactivate();

        await accountRepository.AddAsync(account, cancellationToken);
        await accountRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Account created {UserId} {AccountId}", userId, account.Id);
        return MapToResponse(account, 0m);
    }

    public async Task<AccountResponse?> UpdateAsync(Guid userId, Guid accountId, AccountRequest request, CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.GetByIdAsync(accountId, userId, cancellationToken);
        if (account is null) return null;

        if (await accountRepository.ExistsByNameAsync(userId, request.Name, accountId, cancellationToken))
            throw new ArgumentException("Já existe uma conta com esse nome.");

        account.Update(request.Name, request.Type, request.InitialBalance);
        if (request.IsActive) account.Activate();
        else account.Deactivate();

        await accountRepository.SaveChangesAsync(cancellationToken);

        var net = await accountTransactionRepository.SumSignedAmountByAccountAsync(account.Id, userId, cancellationToken);
        _logger.LogInformation("Account updated {UserId} {AccountId}", userId, account.Id);
        return MapToResponse(account, net);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.GetByIdAsync(accountId, userId, cancellationToken);
        if (account is null) return false;

        accountRepository.Remove(account);
        await accountRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Account deleted {UserId} {AccountId}", userId, accountId);
        return true;
    }

    public async Task<AccountBalanceResponse?> GetBalanceAsync(Guid userId, Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.GetByIdAsync(accountId, userId, cancellationToken);
        if (account is null) return null;

        var net = await accountTransactionRepository.SumSignedAmountByAccountAsync(accountId, userId, cancellationToken);
        return new AccountBalanceResponse(accountId, account.InitialBalance, net, account.InitialBalance + net);
    }

    public async Task<RealAvailableBalanceResponse> GetRealAvailableBalanceAsync(
        Guid userId,
        string period = "month",
        DateOnly? referenceDate = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPeriod = NormalizePeriod(period);
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

        var openExpenseItems = pendingExpenses
            .Where(i => i.Status == InstallmentStatus.Open || i.Status == InstallmentStatus.PartiallyPaid)
            .ToList();

        var pendingExpensesAmount = openExpenseItems.Sum(i => i.Amount);
        var pendingIncomesAmount = pendingIncomes.Sum(i => i.Amount);
        var overdueExpenses = openExpenseItems.Where(i => i.DueDate < today).ToList();
        var dueSoonExpensesAmount = openExpenseItems
            .Where(i => i.DueDate >= today && i.DueDate <= dueSoonLimit)
            .Sum(i => i.Amount);

        return new RealAvailableBalanceResponse(
            normalizedPeriod,
            anchorDate,
            periodStart,
            periodEnd,
            activeAccountsBalance,
            pendingExpensesAmount,
            openExpenseItems.Count,
            pendingIncomesAmount,
            pendingIncomes.Count,
            activeAccountsBalance - pendingExpensesAmount,
            activeAccountsBalance - pendingExpensesAmount + pendingIncomesAmount,
            overdueExpenses.Sum(i => i.Amount),
            overdueExpenses.Count,
            dueSoonExpensesAmount);
    }

    public async Task<DebtSummaryResponse> GetDebtSummaryAsync(
        Guid userId,
        DateOnly? referenceDate = null,
        CancellationToken cancellationToken = default)
    {
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

        return new DebtSummaryResponse(
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
    }

    public async Task<NetWorthSummaryResponse> GetNetWorthSummaryAsync(
        Guid userId,
        DateOnly? referenceDate = null,
        CancellationToken cancellationToken = default)
    {
        var anchorDate = referenceDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var accounts = await accountRepository.ListByUserAsync(userId, cancellationToken);
        decimal accountsBalance = 0m;

        foreach (var account in accounts.Where(a => a.IsActive))
        {
            var net = await accountTransactionRepository.SumSignedAmountByAccountAsync(account.Id, userId, cancellationToken);
            accountsBalance += account.InitialBalance + net;
        }

        var positions = await investmentsService.ListPositionsAsync(userId, cancellationToken);
        var enrichedPositions = await investmentsService.EnrichWithMarketAsync(positions, cancellationToken);
        var activePositions = enrichedPositions.Where(p => p.Quantity > 0).ToList();
        var investmentsBalance = activePositions.Where(IsFinancialInvestment).Sum(CalculatePositionValue);
        var tangibleAssetsBalance = activePositions.Where(IsTangibleAsset).Sum(CalculatePositionValue);

        var openLiabilities = await BuildOpenDebtItemsAsync(userId, cancellationToken);
        var cardDebt = openLiabilities.Where(i => i.Family == "card").Sum(i => i.OpenAmount);
        var totalLiabilities = openLiabilities.Sum(i => i.OpenAmount);
        var otherOpenLiabilities = Math.Max(totalLiabilities - cardDebt, 0m);
        var totalAssets = accountsBalance + investmentsBalance + tangibleAssetsBalance;

        return new NetWorthSummaryResponse(
            anchorDate,
            new WealthAssetBreakdownResponse(accountsBalance, investmentsBalance, tangibleAssetsBalance, totalAssets),
            new WealthLiabilityBreakdownResponse(cardDebt, otherOpenLiabilities, totalLiabilities),
            totalAssets - totalLiabilities,
            activePositions.Count,
            openLiabilities.Count,
            anchorDate.ToString("MM/yyyy"));
    }

    public async Task<NetWorthHistoryResponse> GetNetWorthHistoryAsync(
        Guid userId,
        int months = 12,
        DateOnly? referenceDate = null,
        CancellationToken cancellationToken = default)
    {
        if (months is < 3 or > 24)
            throw new ArgumentException("Quantidade de meses inválida. Use um valor entre 3 e 24.");

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

        var positions = await investmentsService.ListPositionsAsync(userId, cancellationToken);
        var enrichedPositions = await investmentsService.EnrichWithMarketAsync(positions, cancellationToken);

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

        return new NetWorthHistoryResponse(
            anchorDate,
            months,
            hasEstimatedPoints,
            notes,
            points);
    }

    public async Task<CashflowProjectionResponse> GetProjectionAsync(
        Guid userId,
        string period = "month",
        DateOnly? referenceDate = null,
        CancellationToken cancellationToken = default)
    {
        return await cashflowProjectionEngine.ProjectAsync(userId, period, referenceDate, cancellationToken);
    }

    public async Task<RiskBotAssessmentResponse> GetRiskAssessmentAsync(
        Guid userId,
        string period = "month",
        DateOnly? referenceDate = null,
        CancellationToken cancellationToken = default)
    {
        return await riskBotService.AssessAsync(userId, period, referenceDate, cancellationToken);
    }

    public async Task<InsightEngineResponse> GetInsightsAsync(
        Guid userId,
        string period = "month",
        DateOnly? referenceDate = null,
        CancellationToken cancellationToken = default)
    {
        return await insightEngineService.BuildAsync(userId, period, referenceDate, cancellationToken);
    }

    public async Task<RecommendationEngineResponse> GetRecommendationsAsync(
        Guid userId,
        string period = "month",
        DateOnly? referenceDate = null,
        CancellationToken cancellationToken = default)
    {
        return await recommendationEngineService.BuildAsync(userId, period, referenceDate, cancellationToken);
    }

    public async Task<IReadOnlyList<AccountTransactionResponse>?> ListTransactionsAsync(
        Guid userId,
        Guid accountId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.GetByIdAsync(accountId, userId, cancellationToken);
        if (account is null) return null;

        var items = await accountTransactionRepository.ListByAccountAsync(accountId, userId, fromUtc, toUtc, cancellationToken);
        return items.Select(t => new AccountTransactionResponse(
            t.Id,
            t.AccountId,
            t.OccurredAt,
            ResolveTransactionType(t),
            t.Kind,
            t.Amount,
            t.Description,
            t.SourceType,
            ResolveSourceGroup(t.SourceType),
            ResolveSourceLabel(t.SourceType),
            t.SourceId,
            t.CreatedAt)).ToList();
    }

    public async Task<AccountTransferResponse?> TransferAsync(Guid userId, AccountTransferRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
            throw new ArgumentException("Valor da transferência deve ser maior que zero.");
        if (request.FromAccountId == request.ToAccountId)
            throw new ArgumentException("Conta de origem e destino devem ser diferentes.");

        var from = await accountRepository.GetByIdAsync(request.FromAccountId, userId, cancellationToken);
        var to = await accountRepository.GetByIdAsync(request.ToAccountId, userId, cancellationToken);
        if (from is null || to is null) return null;
        if (!from.IsActive) throw new ArgumentException("Conta de origem está inativa.");
        if (!to.IsActive) throw new ArgumentException("Conta de destino está inativa.");

        var occurredAt = (request.OccurredAt ?? DateTime.UtcNow).ToUniversalTime();
        var transferId = Guid.NewGuid();
        var description = string.IsNullOrWhiteSpace(request.Description)
            ? $"Transferência {from.Name} -> {to.Name}"
            : request.Description.Trim();

        await accountTransactionRepository.AddAsync(new AccountTransaction(
            from.Id,
            userId,
            occurredAt,
            Domain.Enums.AccountTransactionKind.Debit,
            request.Amount,
            description,
            sourceType: AccountTransactionSourceTypes.AccountTransfer,
            sourceId: transferId), cancellationToken);

        await accountTransactionRepository.AddAsync(new AccountTransaction(
            to.Id,
            userId,
            occurredAt,
            Domain.Enums.AccountTransactionKind.Credit,
            request.Amount,
            description,
            sourceType: AccountTransactionSourceTypes.AccountTransfer,
            sourceId: transferId), cancellationToken);

        await accountRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Account transfer created {UserId} {TransferId} {FromAccountId} -> {ToAccountId} Amount {Amount}",
            userId,
            transferId,
            from.Id,
            to.Id,
            request.Amount);

        return new AccountTransferResponse(
            transferId,
            from.Id,
            to.Id,
            request.Amount,
            occurredAt,
            description);
    }

    private static AccountResponse MapToResponse(Account account, decimal transactionsNet)
    {
        var current = account.InitialBalance + transactionsNet;
        return new AccountResponse(
            account.Id,
            account.Name,
            account.Type,
            account.InitialBalance,
            current,
            account.IsActive,
            account.CreatedAt,
            account.UpdatedAt);
    }

    private static AccountTransactionType ResolveTransactionType(AccountTransaction transaction)
    {
        if (string.Equals(transaction.SourceType, AccountTransactionSourceTypes.AccountTransfer, StringComparison.OrdinalIgnoreCase))
            return AccountTransactionType.Transfer;

        return transaction.Kind == Domain.Enums.AccountTransactionKind.Credit
            ? AccountTransactionType.Income
            : AccountTransactionType.Expense;
    }

    private static string? ResolveSourceGroup(string? sourceType)
    {
        return sourceType switch
        {
            AccountTransactionSourceTypes.InstallmentPayment => "FinancialEntry",
            AccountTransactionSourceTypes.InstallmentPaymentReversal => "FinancialEntryReversal",
            AccountTransactionSourceTypes.AccountTransfer => "Transfer",
            AccountTransactionSourceTypes.BankStatementImport => "Import",
            _ => string.IsNullOrWhiteSpace(sourceType) ? null : "Other"
        };
    }

    private static string? ResolveSourceLabel(string? sourceType)
    {
        return sourceType switch
        {
            AccountTransactionSourceTypes.InstallmentPayment => "Receita/Despesa",
            AccountTransactionSourceTypes.InstallmentPaymentReversal => "Estorno",
            AccountTransactionSourceTypes.AccountTransfer => "Transferência",
            AccountTransactionSourceTypes.BankStatementImport => "Importação de extrato",
            _ => string.IsNullOrWhiteSpace(sourceType) ? null : sourceType
        };
    }

    private static string NormalizePeriod(string? period)
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
        var payments = await moneyPaymentRepository.ListByInstallmentIdsAsync(openInstallments.Select(i => i.Id), cancellationToken);

        var planLookup = plans.ToDictionary(p => p.Id, p => p);
        var cardLookup = cards.ToDictionary(c => c.Id, c => string.IsNullOrWhiteSpace(c.Nickname) ? c.HolderName : c.Nickname);
        var paidLookup = payments
            .GroupBy(p => p.InstallmentId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.PaidAmount));

        return openInstallments
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
    }
}
