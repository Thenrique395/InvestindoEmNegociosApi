using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Entities;

public class LoanContract
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public decimal PrincipalAmount { get; private set; }
    public decimal AnnualInterestRate { get; private set; }
    public int TermMonths { get; private set; }
    public LoanAmortizationType AmortizationType { get; private set; }
    public DateOnly StartDate { get; private set; }
    public int PaymentDay { get; private set; }
    public decimal MonthlyPayment { get; private set; }
    public decimal TotalCost { get; private set; }
    public decimal TotalInterest { get; private set; }
    public LoanStatus Status { get; private set; } = LoanStatus.Active;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private LoanContract() { }

    public LoanContract(
        Guid userId,
        string title,
        decimal principalAmount,
        decimal annualInterestRate,
        int termMonths,
        LoanAmortizationType amortizationType,
        DateOnly startDate,
        int paymentDay,
        decimal monthlyPayment,
        decimal totalCost,
        decimal totalInterest)
    {
        UserId = userId;
        Title = title;
        PrincipalAmount = principalAmount;
        AnnualInterestRate = annualInterestRate;
        TermMonths = termMonths;
        AmortizationType = amortizationType;
        StartDate = startDate;
        PaymentDay = paymentDay;
        MonthlyPayment = monthlyPayment;
        TotalCost = totalCost;
        TotalInterest = totalInterest;
    }

    public void MarkClosed()
    {
        Status = LoanStatus.Closed;
        UpdatedAt = DateTime.UtcNow;
    }
}
