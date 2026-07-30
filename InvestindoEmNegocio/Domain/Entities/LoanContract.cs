using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Entities;

public class LoanContract
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Guid SpaceId { get; private set; }

    // Identificação
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid? InstitutionId { get; private set; }
    public string? InstitutionName { get; private set; }
    public LoanContractType ContractType { get; private set; } = LoanContractType.Other;

    // Valores
    public decimal PrincipalAmount { get; private set; }
    public decimal? AssetAmount { get; private set; }
    public decimal? DownPaymentAmount { get; private set; }
    public decimal FinancedAmount { get; private set; }

    // Taxas
    public decimal AnnualInterestRate { get; private set; }
    public decimal MonthlyInterestRate { get; private set; }
    public InterestRatePeriod InterestRatePeriod { get; private set; } = InterestRatePeriod.AnnualNominal;
    public decimal? EffectiveAnnualRate { get; private set; }
    public decimal? CetRate { get; private set; }

    // Prazo
    public int TermMonths { get; private set; }
    public int OriginalTermMonths { get; private set; }
    public int GracePeriodMonths { get; private set; }
    public LoanAmortizationType AmortizationType { get; private set; }
    public DateOnly StartDate { get; private set; }
    public int PaymentDay { get; private set; }

    // Resultados do cronograma
    public decimal MonthlyPayment { get; private set; }
    public decimal TotalCost { get; private set; }
    public decimal TotalInterest { get; private set; }

    // Acompanhamento (materializado; fonte oficial é o backend)
    public decimal OpenBalance { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal PaidPrincipal { get; private set; }
    public decimal PaidInterest { get; private set; }

    public LoanStatus Status { get; private set; } = LoanStatus.Active;
    public DateTime? ClosedAt { get; private set; }
    public DateTime? ArchivedAt { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    /// <summary>Token de concorrência otimista (mesmo padrão portável de LoanInstallment).</summary>
    public int Version { get; private set; }

    private LoanContract() { }

    public LoanContract(
        Guid userId,
        Guid spaceId,
        string title,
        decimal principalAmount,
        decimal annualInterestRate,
        decimal monthlyInterestRate,
        InterestRatePeriod interestRatePeriod,
        int termMonths,
        LoanAmortizationType amortizationType,
        DateOnly startDate,
        int paymentDay,
        decimal monthlyPayment,
        decimal totalCost,
        decimal totalInterest,
        decimal openBalance,
        LoanContractType contractType = LoanContractType.Other,
        string? description = null,
        Guid? institutionId = null,
        string? institutionName = null,
        decimal? assetAmount = null,
        decimal? downPaymentAmount = null,
        decimal? financedAmount = null,
        decimal? effectiveAnnualRate = null,
        decimal? cetRate = null,
        int gracePeriodMonths = 0)
    {
        UserId = userId;
        SpaceId = spaceId;
        Title = title;
        ContractType = contractType;
        Description = description;
        InstitutionId = institutionId;
        InstitutionName = institutionName;
        PrincipalAmount = principalAmount;
        AssetAmount = assetAmount;
        DownPaymentAmount = downPaymentAmount;
        FinancedAmount = financedAmount ?? principalAmount;
        AnnualInterestRate = annualInterestRate;
        MonthlyInterestRate = monthlyInterestRate;
        InterestRatePeriod = interestRatePeriod;
        EffectiveAnnualRate = effectiveAnnualRate;
        CetRate = cetRate;
        TermMonths = termMonths;
        OriginalTermMonths = termMonths;
        GracePeriodMonths = gracePeriodMonths;
        AmortizationType = amortizationType;
        StartDate = startDate;
        PaymentDay = paymentDay;
        MonthlyPayment = monthlyPayment;
        TotalCost = totalCost;
        TotalInterest = totalInterest;
        OpenBalance = openBalance;
    }

    /// <summary>Atualiza os dados do contrato e os resultados do cronograma recalculado.</summary>
    public void Update(
        string title,
        decimal principalAmount,
        decimal annualInterestRate,
        decimal monthlyInterestRate,
        InterestRatePeriod interestRatePeriod,
        int termMonths,
        LoanAmortizationType amortizationType,
        DateOnly startDate,
        int paymentDay,
        decimal monthlyPayment,
        decimal totalCost,
        decimal totalInterest,
        decimal openBalance)
    {
        Title = title;
        PrincipalAmount = principalAmount;
        FinancedAmount = principalAmount;
        AnnualInterestRate = annualInterestRate;
        MonthlyInterestRate = monthlyInterestRate;
        InterestRatePeriod = interestRatePeriod;
        TermMonths = termMonths;
        AmortizationType = amortizationType;
        StartDate = startDate;
        PaymentDay = paymentDay;
        MonthlyPayment = monthlyPayment;
        TotalCost = totalCost;
        TotalInterest = totalInterest;
        OpenBalance = openBalance;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    /// <summary>Atualiza os totais de acompanhamento a partir dos pagamentos e parcelas em aberto.</summary>
    public void UpdateTracking(decimal openBalance, decimal paidAmount, decimal paidPrincipal, decimal paidInterest)
    {
        OpenBalance = openBalance < 0 ? 0m : openBalance;
        PaidAmount = paidAmount;
        PaidPrincipal = paidPrincipal;
        PaidInterest = paidInterest;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    /// <summary>Marca o contrato como quitado (saldo zero).</summary>
    public void MarkClosed()
    {
        Status = LoanStatus.Closed;
        OpenBalance = 0m;
        ClosedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    /// <summary>Aplica o efeito de uma amortização extraordinária (novo saldo e nova parcela).</summary>
    public void ApplyAmortization(decimal openBalance, decimal monthlyPayment)
    {
        OpenBalance = openBalance < 0 ? 0m : openBalance;
        MonthlyPayment = monthlyPayment;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    /// <summary>Reabre um contrato quitado (ex.: estorno de pagamento reabre uma parcela).</summary>
    public void Reopen()
    {
        Status = LoanStatus.Active;
        ClosedAt = null;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    /// <summary>Arquiva o contrato, preservando todo o histórico (não é exclusão).</summary>
    public void Archive()
    {
        ArchivedAt = DateTime.UtcNow;
        Status = LoanStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    /// <summary>Cancela o contrato (ex.: criado por engano), preservando o histórico.</summary>
    public void Cancel()
    {
        Status = LoanStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }
}
