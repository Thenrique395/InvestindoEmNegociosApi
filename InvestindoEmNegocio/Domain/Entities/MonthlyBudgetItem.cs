namespace InvestindoEmNegocio.Domain.Entities;

public class MonthlyBudgetItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public string CategoryName { get; private set; } = string.Empty;
    public decimal PlannedAmount { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private MonthlyBudgetItem() { }

    public MonthlyBudgetItem(Guid userId, int year, int month, string categoryName, decimal plannedAmount)
    {
        UserId = userId;
        Year = year;
        Month = month;
        CategoryName = categoryName;
        PlannedAmount = plannedAmount;
    }

    public void Update(decimal plannedAmount)
    {
        PlannedAmount = plannedAmount;
        UpdatedAt = DateTime.UtcNow;
    }
}
