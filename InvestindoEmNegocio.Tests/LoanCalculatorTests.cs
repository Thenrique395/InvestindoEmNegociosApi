using FluentAssertions;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Finance;

namespace InvestindoEmNegocio.Tests;

public class LoanCalculatorTests
{
    // ------------------------------------------------------------------
    // Invariantes universais (valem para qualquer cronograma válido).
    // ------------------------------------------------------------------

    private static void AssertInvariants(LoanSchedule schedule, decimal principal, int termMonths)
    {
        schedule.Rows.Should().HaveCount(termMonths);
        schedule.Rows[^1].EndingBalance.Should().Be(0m, "o saldo devedor deve fechar em zero");
        schedule.Rows.Sum(r => r.PrincipalAmount).Should().Be(principal, "a soma das amortizações deve igualar o principal");
        schedule.Rows.Sum(r => r.TotalAmount).Should().Be(schedule.TotalCost, "a soma das parcelas deve igualar o custo total");
        schedule.TotalCost.Should().Be(LoanCalculator.RoundMoney(principal + schedule.TotalInterest),
            "custo total = principal + juros totais");
        schedule.Rows.Sum(r => r.InterestAmount).Should().Be(schedule.TotalInterest);

        // Encadeamento de saldos: saldo_inicial_k == saldo_final_{k-1}; saldo_final == saldo_inicial - amortização.
        for (var i = 0; i < schedule.Rows.Count; i++)
        {
            var row = schedule.Rows[i];
            row.InstallmentNo.Should().Be(i + 1);
            row.EndingBalance.Should().Be(row.BeginningBalance - row.PrincipalAmount);
            if (i > 0)
                row.BeginningBalance.Should().Be(schedule.Rows[i - 1].EndingBalance);
        }
        schedule.Rows[0].BeginningBalance.Should().Be(principal);
    }

    // ------------------------------------------------------------------
    // PRICE
    // ------------------------------------------------------------------

    [Fact]
    public void Price_With_Zero_Rate_Splits_Principal_Evenly()
    {
        var schedule = LoanCalculator.Build(12000m, 0m, 12, LoanAmortizationType.Price);

        AssertInvariants(schedule, 12000m, 12);
        schedule.TotalInterest.Should().Be(0m);
        schedule.Rows.Should().OnlyContain(r => r.TotalAmount == 1000m);
        schedule.MonthlyRate.Should().Be(0m);
    }

    [Fact]
    public void Price_With_Known_Rate_Matches_Standard_Payment()
    {
        // PV = 1000, i = 1% a.m., n = 12 → PMT = 1000 · 0,01 / (1 − 1,01^-12) ≈ 88,85
        var schedule = LoanCalculator.Build(1000m, 0.01m, 12, LoanAmortizationType.Price);

        AssertInvariants(schedule, 1000m, 12);
        schedule.FirstPayment.Should().BeApproximately(88.85m, 0.01m);
        schedule.Rows[0].InterestAmount.Should().Be(10.00m); // 1000 · 1%
        // PRICE: parcela ~constante e amortização crescente.
        schedule.Rows[1].PrincipalAmount.Should().BeGreaterThan(schedule.Rows[0].PrincipalAmount);
        schedule.TotalInterest.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void Price_Interest_Is_Charged_On_Outstanding_Balance()
    {
        var schedule = LoanCalculator.Build(5000m, 0.02m, 10, LoanAmortizationType.Price);
        AssertInvariants(schedule, 5000m, 10);
        // Juros decrescentes (saldo cai).
        for (var i = 1; i < schedule.Rows.Count; i++)
            schedule.Rows[i].InterestAmount.Should().BeLessThanOrEqualTo(schedule.Rows[i - 1].InterestAmount);
    }

    // ------------------------------------------------------------------
    // SAC
    // ------------------------------------------------------------------

    [Fact]
    public void Sac_With_Zero_Rate_Splits_Principal_Evenly()
    {
        var schedule = LoanCalculator.Build(12000m, 0m, 12, LoanAmortizationType.Sac);

        AssertInvariants(schedule, 12000m, 12);
        schedule.TotalInterest.Should().Be(0m);
        schedule.Rows.Should().OnlyContain(r => r.PrincipalAmount == 1000m && r.TotalAmount == 1000m);
    }

    [Fact]
    public void Sac_With_Known_Rate_Has_Constant_Principal_And_Decreasing_Payment()
    {
        // PV = 1200, i = 1% a.m., n = 12 → amortização constante = 100; juros mês 1 = 12; parcela 1 = 112.
        var schedule = LoanCalculator.Build(1200m, 0.01m, 12, LoanAmortizationType.Sac);

        AssertInvariants(schedule, 1200m, 12);
        schedule.Rows[0].PrincipalAmount.Should().Be(100m);
        schedule.Rows[0].InterestAmount.Should().Be(12m);
        schedule.Rows[0].TotalAmount.Should().Be(112m);
        // Parcela decrescente.
        for (var i = 1; i < schedule.Rows.Count; i++)
            schedule.Rows[i].TotalAmount.Should().BeLessThanOrEqualTo(schedule.Rows[i - 1].TotalAmount);
        schedule.FirstPayment.Should().BeGreaterThan(schedule.LastPayment);
    }

    // ------------------------------------------------------------------
    // Bordas: prazo curto/longo, centavos, saldo final zero.
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(500, 0.02, 1)]
    [InlineData(1000, 0.015, 1)]
    public void Single_Month_Term_Closes_In_One_Installment(decimal principal, decimal rate, int term)
    {
        var price = LoanCalculator.Build(principal, rate, term, LoanAmortizationType.Price);
        var sac = LoanCalculator.Build(principal, rate, term, LoanAmortizationType.Sac);

        AssertInvariants(price, principal, term);
        AssertInvariants(sac, principal, term);
        price.Rows[0].PrincipalAmount.Should().Be(principal);
        sac.Rows[0].PrincipalAmount.Should().Be(principal);
    }

    [Theory]
    [InlineData(360)]
    [InlineData(480)]
    public void Long_Term_Still_Closes_Balance_At_Zero(int term)
    {
        var schedule = LoanCalculator.Build(250000m, 0.008m, term, LoanAmortizationType.Price);
        AssertInvariants(schedule, 250000m, term);
    }

    [Theory]
    [InlineData(1000.01, 0.0137, 7, LoanAmortizationType.Price)]
    [InlineData(3333.33, 0.019, 11, LoanAmortizationType.Sac)]
    [InlineData(9999.99, 0.0233, 13, LoanAmortizationType.Price)]
    [InlineData(12345.67, 0.011, 24, LoanAmortizationType.Sac)]
    public void Cents_Residual_Is_Absorbed_By_Last_Installment(decimal principal, decimal rate, int term, LoanAmortizationType system)
    {
        var schedule = LoanCalculator.Build(principal, rate, term, system);
        AssertInvariants(schedule, principal, term);
    }

    // ------------------------------------------------------------------
    // Conversões de taxa (mensal ↔ anual, nominal vs efetiva).
    // ------------------------------------------------------------------

    [Fact]
    public void Nominal_Annual_To_Monthly_Is_Linear()
    {
        LoanCalculator.MonthlyRateFromAnnualNominal(12m).Should().Be(0.01m);
        LoanCalculator.AnnualNominalFromMonthlyRate(0.01m).Should().Be(0.12m);
    }

    [Fact]
    public void Effective_Annual_And_Monthly_Are_Compound_Inverses()
    {
        // 1% a.m. → ~12,6825% a.a. efetivo; e volta.
        var annualEffective = LoanCalculator.AnnualEffectiveFromMonthlyRate(0.01m);
        ((double)annualEffective).Should().BeApproximately(0.126825, 0.00001);

        var monthly = LoanCalculator.MonthlyRateFromAnnualEffective(annualEffective * 100m);
        ((double)monthly).Should().BeApproximately(0.01, 0.00001);
    }

    [Fact]
    public void Nominal_And_Effective_Conversions_Differ()
    {
        // Para a mesma taxa anual, a mensal nominal é maior que a mensal efetiva (composição).
        var nominalMonthly = LoanCalculator.MonthlyRateFromAnnualNominal(12m);
        var effectiveMonthly = LoanCalculator.MonthlyRateFromAnnualEffective(12m);
        nominalMonthly.Should().BeGreaterThan(effectiveMonthly);
    }

    // ------------------------------------------------------------------
    // Guardas.
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(0, 0.01, 12)]
    [InlineData(-100, 0.01, 12)]
    public void Rejects_NonPositive_Principal(decimal principal, decimal rate, int term)
    {
        var act = () => LoanCalculator.Build(principal, rate, term, LoanAmortizationType.Price);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Rejects_Negative_Rate()
    {
        var act = () => LoanCalculator.Build(1000m, -0.01m, 12, LoanAmortizationType.Price);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Rejects_Invalid_Term(int term)
    {
        var act = () => LoanCalculator.Build(1000m, 0.01m, term, LoanAmortizationType.Price);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ------------------------------------------------------------------
    // Amortização extraordinária.
    // ------------------------------------------------------------------

    [Fact]
    public void Amortization_ReduceTerm_Keeps_Payment_And_Shortens_Schedule()
    {
        var payment = LoanCalculator.Build(10000m, 0.01m, 12, LoanAmortizationType.Price).FirstPayment;

        var outcome = LoanCalculator.SimulateExtraordinary(
            10000m, 0.01m, 12, payment, LoanAmortizationType.Price, 2000m, LoanAmortizationStrategy.ReduceTerm);

        outcome.NewBalance.Should().Be(8000m);
        outcome.NewPayment.Should().Be(payment);
        outcome.NewTerm.Should().BeLessThan(12);
        outcome.EstimatedSavings.Should().BeGreaterThan(0m);
        outcome.NewSchedule.Rows[^1].EndingBalance.Should().Be(0m);
        outcome.NewSchedule.Rows.Sum(r => r.PrincipalAmount).Should().Be(8000m);
    }

    [Fact]
    public void Amortization_ReducePayment_Keeps_Term_And_Lowers_Payment()
    {
        var payment = LoanCalculator.Build(10000m, 0.01m, 12, LoanAmortizationType.Price).FirstPayment;

        var outcome = LoanCalculator.SimulateExtraordinary(
            10000m, 0.01m, 12, payment, LoanAmortizationType.Price, 2000m, LoanAmortizationStrategy.ReducePayment);

        outcome.NewBalance.Should().Be(8000m);
        outcome.NewTerm.Should().Be(12);
        outcome.NewPayment.Should().BeLessThan(payment);
        outcome.EstimatedSavings.Should().BeGreaterThan(0m);
        outcome.NewSchedule.Rows.Should().HaveCount(12);
        outcome.NewSchedule.Rows[^1].EndingBalance.Should().Be(0m);
        outcome.NewSchedule.Rows.Sum(r => r.PrincipalAmount).Should().Be(8000m);
    }

    [Fact]
    public void Amortization_FullSettlement_Zeroes_Balance_And_Saves_All_Future_Interest()
    {
        var payment = LoanCalculator.Build(10000m, 0.01m, 12, LoanAmortizationType.Price).FirstPayment;

        var outcome = LoanCalculator.SimulateExtraordinary(
            10000m, 0.01m, 12, payment, LoanAmortizationType.Price, 10000m, LoanAmortizationStrategy.FullSettlement);

        outcome.NewBalance.Should().Be(0m);
        outcome.NewTerm.Should().Be(0);
        outcome.NewSchedule.Rows.Should().BeEmpty();
        outcome.EstimatedSavings.Should().Be(outcome.EstimatedInterestBefore);
        outcome.EstimatedInterestBefore.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void Amortization_Amount_Above_Balance_Becomes_Full_Settlement()
    {
        var payment = LoanCalculator.Build(10000m, 0.01m, 12, LoanAmortizationType.Price).FirstPayment;

        var outcome = LoanCalculator.SimulateExtraordinary(
            10000m, 0.01m, 12, payment, LoanAmortizationType.Price, 12000m, LoanAmortizationStrategy.ReduceTerm);

        outcome.Strategy.Should().Be(LoanAmortizationStrategy.FullSettlement);
        outcome.NewBalance.Should().Be(0m);
    }

    [Fact]
    public void Amortization_Rejects_NonPositive_Amount()
    {
        var act = () => LoanCalculator.SimulateExtraordinary(
            10000m, 0.01m, 12, 900m, LoanAmortizationType.Price, 0m, LoanAmortizationStrategy.ReduceTerm);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
