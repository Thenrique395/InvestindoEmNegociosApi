namespace InvestindoEmNegocio.Domain.Entities;

public class MonthlyFinancialSnapshot
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public string SnapshotLabel { get; private set; } = string.Empty;
    public decimal RealAvailableBalance { get; private set; }
    public decimal ProjectedBalance { get; private set; }
    public decimal PendingExpenses { get; private set; }
    public decimal PendingIncomes { get; private set; }
    public decimal TotalDebt { get; private set; }
    public decimal NetWorth { get; private set; }
    public int RiskScore { get; private set; }
    public string RiskClassification { get; private set; } = string.Empty;
    public string PrimaryInsight { get; private set; } = string.Empty;
    public string RecommendationsJson { get; private set; } = "[]";
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private MonthlyFinancialSnapshot() { }

    public MonthlyFinancialSnapshot(
        Guid userId,
        int year,
        int month,
        decimal realAvailableBalance,
        decimal projectedBalance,
        decimal pendingExpenses,
        decimal pendingIncomes,
        decimal totalDebt,
        decimal netWorth,
        int riskScore,
        string riskClassification,
        string primaryInsight,
        string recommendationsJson)
    {
        UserId = userId;
        Year = year;
        Month = month;
        SnapshotLabel = $"{month:00}/{year:0000}";
        RealAvailableBalance = realAvailableBalance;
        ProjectedBalance = projectedBalance;
        PendingExpenses = pendingExpenses;
        PendingIncomes = pendingIncomes;
        TotalDebt = totalDebt;
        NetWorth = netWorth;
        RiskScore = riskScore;
        RiskClassification = riskClassification;
        PrimaryInsight = primaryInsight;
        RecommendationsJson = recommendationsJson;
    }
}
