using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Finance;

/// <summary>
/// Uma linha do cronograma de amortização (sem data — datas são responsabilidade da camada
/// de aplicação, mantendo o cálculo puro e livre de fuso horário).
/// </summary>
public sealed record LoanScheduleRow(
    int InstallmentNo,
    decimal BeginningBalance,
    decimal PrincipalAmount,
    decimal InterestAmount,
    decimal TotalAmount,
    decimal EndingBalance);

/// <summary>Estratégia de amortização extraordinária.</summary>
public enum LoanAmortizationStrategy
{
    /// <summary>Mantém a parcela e reduz o prazo (quita antes).</summary>
    ReduceTerm = 1,
    /// <summary>Mantém o prazo e reduz a parcela.</summary>
    ReducePayment = 2,
    /// <summary>Quita todo o saldo devedor.</summary>
    FullSettlement = 3
}

/// <summary>
/// Estimativa de uma amortização extraordinária: saldos/prazo/parcela antes e depois e a
/// economia estimada de juros. É uma ESTIMATIVA — o cronograma oficial deve ser confirmado
/// com a instituição financeira.
/// </summary>
public sealed record LoanAmortizationOutcome(
    LoanAmortizationStrategy Strategy,
    decimal AmortizationAmount,
    decimal PreviousBalance,
    decimal NewBalance,
    int PreviousTerm,
    int NewTerm,
    decimal PreviousPayment,
    decimal NewPayment,
    decimal EstimatedInterestBefore,
    decimal EstimatedInterestAfter,
    decimal EstimatedSavings,
    LoanSchedule NewSchedule);

/// <summary>Resultado completo de uma amortização (cronograma + resumo).</summary>
public sealed record LoanSchedule(
    LoanAmortizationType System,
    decimal Principal,
    decimal MonthlyRate,
    int TermMonths,
    decimal FirstPayment,
    decimal LastPayment,
    decimal AveragePayment,
    decimal TotalCost,
    decimal TotalInterest,
    IReadOnlyList<LoanScheduleRow> Rows);

/// <summary>
/// Calculadora de amortização pura e determinística (PRICE e SAC), fonte oficial dos cálculos
/// financeiros de empréstimos e financiamentos.
///
/// <para><b>Política de arredondamento:</b> half-up (<see cref="MidpointRounding.AwayFromZero"/>)
/// com 2 casas para valores monetários. A <b>última parcela absorve o resíduo de centavos</b>,
/// garantindo que:</para>
/// <list type="bullet">
///   <item>o saldo devedor final feche em exatamente 0,00;</item>
///   <item>a soma das amortizações (principal) seja igual ao principal financiado;</item>
///   <item>a soma das parcelas seja igual ao custo total (principal + juros).</item>
/// </list>
///
/// <para><b>Taxas:</b> o cálculo opera sempre com a <b>taxa mensal</b> explícita. A conversão de
/// taxa anual para mensal é responsabilidade de quem chama, usando um dos helpers abaixo — nunca
/// se presume se um valor informado é mensal ou anual, nem se a taxa anual é nominal ou efetiva.</para>
///
/// <para><b>Fórmulas:</b><br/>
/// PRICE (parcela constante): PMT = PV · i / (1 − (1 + i)^−n); juros_k = saldo_{k−1} · i;
/// amortização_k = PMT − juros_k.<br/>
/// SAC (amortização constante): amortização_k = PV / n; juros_k = saldo_{k−1} · i;
/// parcela_k = amortização_k + juros_k.</para>
/// </summary>
public static class LoanCalculator
{
    /// <summary>Casas decimais para valores monetários.</summary>
    public const int MoneyDecimals = 2;

    /// <summary>Política de arredondamento oficial: half-up.</summary>
    public const MidpointRounding MoneyRounding = MidpointRounding.AwayFromZero;

    /// <summary>Arredonda um valor monetário conforme a política oficial (half-up, 2 casas).</summary>
    public static decimal RoundMoney(decimal value) => Math.Round(value, MoneyDecimals, MoneyRounding);

    // ------------------------------------------------------------------
    // Conversões de taxa (período/base explícitos — nunca presumidos).
    // ------------------------------------------------------------------

    /// <summary>Taxa mensal a partir de uma taxa anual <b>nominal</b> (linear): i = anual / 12.</summary>
    public static decimal MonthlyRateFromAnnualNominal(decimal annualPercent) => annualPercent / 12m / 100m;

    /// <summary>Taxa mensal a partir de uma taxa anual <b>efetiva</b> (composta): i = (1 + anual)^(1/12) − 1.</summary>
    public static decimal MonthlyRateFromAnnualEffective(decimal annualPercent)
        => (decimal)Math.Pow(1d + (double)(annualPercent / 100m), 1d / 12d) - 1m;

    /// <summary>Taxa anual <b>efetiva</b> (composta) a partir de uma taxa mensal: (1 + i)^12 − 1.</summary>
    public static decimal AnnualEffectiveFromMonthlyRate(decimal monthlyRate)
        => (decimal)Math.Pow(1d + (double)monthlyRate, 12d) - 1m;

    /// <summary>Taxa anual <b>nominal</b> (linear) a partir de uma taxa mensal: i · 12.</summary>
    public static decimal AnnualNominalFromMonthlyRate(decimal monthlyRate) => monthlyRate * 12m;

    // ------------------------------------------------------------------
    // Cronograma
    // ------------------------------------------------------------------

    /// <summary>
    /// Gera o cronograma de amortização para o sistema informado.
    /// </summary>
    /// <param name="principal">Valor financiado (principal). Deve ser &gt; 0.</param>
    /// <param name="monthlyRate">Taxa de juros <b>mensal</b> em fração (ex.: 0.015 = 1,5% a.m.). Deve ser &gt;= 0.</param>
    /// <param name="termMonths">Prazo em meses. Deve ser &gt;= 1.</param>
    /// <param name="system">Sistema de amortização (PRICE ou SAC).</param>
    public static LoanSchedule Build(decimal principal, decimal monthlyRate, int termMonths, LoanAmortizationType system)
    {
        if (principal <= 0)
            throw new ArgumentOutOfRangeException(nameof(principal), "Principal deve ser maior que zero.");
        if (monthlyRate < 0)
            throw new ArgumentOutOfRangeException(nameof(monthlyRate), "Taxa mensal não pode ser negativa.");
        if (termMonths < 1)
            throw new ArgumentOutOfRangeException(nameof(termMonths), "Prazo deve ser de ao menos 1 mês.");

        return system == LoanAmortizationType.Sac
            ? BuildSac(principal, monthlyRate, termMonths)
            : BuildPrice(principal, monthlyRate, termMonths);
    }

    private static LoanSchedule BuildPrice(decimal principal, decimal monthlyRate, int termMonths)
    {
        var payment = monthlyRate <= 0
            ? RoundMoney(principal / termMonths)
            : RoundMoney(principal * (monthlyRate / (1m - (decimal)Math.Pow(1d + (double)monthlyRate, -termMonths))));

        var rows = new List<LoanScheduleRow>(termMonths);
        var balance = principal;
        decimal totalCost = 0m;
        decimal totalInterest = 0m;

        for (var n = 1; n <= termMonths; n++)
        {
            var beginning = balance;
            var interest = RoundMoney(beginning * monthlyRate);
            decimal principalPart;
            decimal installment;

            if (n == termMonths)
            {
                // Última parcela: amortiza todo o saldo restante (absorve o resíduo de centavos).
                principalPart = beginning;
                installment = RoundMoney(principalPart + interest);
            }
            else
            {
                installment = payment;
                principalPart = RoundMoney(installment - interest);
                if (principalPart > beginning)
                    principalPart = beginning;
            }

            balance = beginning - principalPart;
            if (balance < 0)
                balance = 0m;

            totalCost += installment;
            totalInterest += interest;
            rows.Add(new LoanScheduleRow(n, beginning, principalPart, interest, installment, balance));
        }

        return Summarize(LoanAmortizationType.Price, principal, monthlyRate, termMonths, rows, totalCost, totalInterest);
    }

    private static LoanSchedule BuildSac(decimal principal, decimal monthlyRate, int termMonths)
    {
        var basePrincipal = RoundMoney(principal / termMonths);

        var rows = new List<LoanScheduleRow>(termMonths);
        var balance = principal;
        decimal totalCost = 0m;
        decimal totalInterest = 0m;

        for (var n = 1; n <= termMonths; n++)
        {
            var beginning = balance;
            var interest = RoundMoney(beginning * monthlyRate);
            // Última parcela: amortiza todo o saldo restante (absorve o resíduo de centavos).
            var principalPart = n == termMonths ? beginning : basePrincipal;
            if (principalPart > beginning)
                principalPart = beginning;
            var installment = RoundMoney(principalPart + interest);

            balance = beginning - principalPart;
            if (balance < 0)
                balance = 0m;

            totalCost += installment;
            totalInterest += interest;
            rows.Add(new LoanScheduleRow(n, beginning, principalPart, interest, installment, balance));
        }

        return Summarize(LoanAmortizationType.Sac, principal, monthlyRate, termMonths, rows, totalCost, totalInterest);
    }

    /// <summary>
    /// Estima o efeito de uma amortização extraordinária sobre o saldo remanescente. Compara o
    /// cronograma futuro atual com o novo cronograma após aplicar o valor amortizado, segundo a
    /// estratégia (reduzir prazo / reduzir parcela / quitar). Retorna também a economia de juros.
    /// ESTIMATIVA — o cronograma oficial deve ser confirmado com a instituição.
    /// </summary>
    /// <param name="currentBalance">Saldo devedor atual (principal remanescente). &gt; 0.</param>
    /// <param name="monthlyRate">Taxa mensal em fração. &gt;= 0.</param>
    /// <param name="remainingTerm">Nº de parcelas em aberto. &gt;= 1.</param>
    /// <param name="currentPayment">Valor da parcela atual (usado no ReduceTerm).</param>
    /// <param name="system">Sistema de amortização do contrato (para ReducePayment).</param>
    /// <param name="amortizationAmount">Valor amortizado extraordinariamente. &gt; 0.</param>
    /// <param name="strategy">Estratégia desejada.</param>
    public static LoanAmortizationOutcome SimulateExtraordinary(
        decimal currentBalance,
        decimal monthlyRate,
        int remainingTerm,
        decimal currentPayment,
        LoanAmortizationType system,
        decimal amortizationAmount,
        LoanAmortizationStrategy strategy)
    {
        if (currentBalance <= 0)
            throw new ArgumentOutOfRangeException(nameof(currentBalance), "Saldo devedor deve ser maior que zero.");
        if (monthlyRate < 0)
            throw new ArgumentOutOfRangeException(nameof(monthlyRate), "Taxa mensal não pode ser negativa.");
        if (remainingTerm < 1)
            throw new ArgumentOutOfRangeException(nameof(remainingTerm), "Prazo remanescente deve ser de ao menos 1 mês.");
        if (amortizationAmount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amortizationAmount), "Valor amortizado deve ser maior que zero.");

        // Juros futuros do cronograma remanescente atual (base para a economia).
        var before = Build(currentBalance, monthlyRate, remainingTerm, system);
        var interestBefore = before.TotalInterest;

        var newBalance = RoundMoney(currentBalance - amortizationAmount);

        // Quitação total (valor >= saldo, ou estratégia explícita).
        if (strategy == LoanAmortizationStrategy.FullSettlement || newBalance <= 0)
        {
            var empty = new LoanSchedule(system, currentBalance, monthlyRate, 0, 0m, 0m, 0m, 0m, 0m, Array.Empty<LoanScheduleRow>());
            return new LoanAmortizationOutcome(
                LoanAmortizationStrategy.FullSettlement,
                Math.Min(amortizationAmount, currentBalance),
                currentBalance, 0m, remainingTerm, 0, currentPayment, 0m,
                interestBefore, 0m, interestBefore, empty);
        }

        LoanSchedule after;
        int newTerm;
        decimal newPayment;

        if (strategy == LoanAmortizationStrategy.ReduceTerm)
        {
            after = BuildFixedPayment(newBalance, monthlyRate, currentPayment);
            newTerm = after.Rows.Count;
            newPayment = currentPayment;
        }
        else
        {
            after = Build(newBalance, monthlyRate, remainingTerm, system);
            newTerm = remainingTerm;
            newPayment = after.FirstPayment;
        }

        var interestAfter = after.TotalInterest;
        return new LoanAmortizationOutcome(
            strategy, amortizationAmount, currentBalance, newBalance, remainingTerm, newTerm,
            currentPayment, newPayment, interestBefore, interestAfter,
            RoundMoney(interestBefore - interestAfter), after);
    }

    /// <summary>Cronograma com parcela fixa (PRICE-like) até o saldo zerar — usado no ReduceTerm.</summary>
    private static LoanSchedule BuildFixedPayment(decimal balance, decimal monthlyRate, decimal payment)
    {
        var rows = new List<LoanScheduleRow>();
        var startingBalance = balance;
        decimal totalCost = 0m;
        decimal totalInterest = 0m;

        var n = 0;
        while (balance > 0 && n < 1200)
        {
            n++;
            var beginning = balance;
            var interest = RoundMoney(beginning * monthlyRate);
            var principalPart = RoundMoney(payment - interest);
            if (principalPart <= 0)
                throw new ArgumentException("A parcela atual é insuficiente para amortizar o saldo.");

            decimal installment = payment;
            if (principalPart >= beginning)
            {
                principalPart = beginning;
                installment = RoundMoney(principalPart + interest);
            }

            balance = beginning - principalPart;
            if (balance < 0)
                balance = 0m;

            totalCost += installment;
            totalInterest += interest;
            rows.Add(new LoanScheduleRow(n, beginning, principalPart, interest, installment, balance));
        }

        return Summarize(LoanAmortizationType.Price, startingBalance, monthlyRate, rows.Count, rows, totalCost, totalInterest);
    }

    private static LoanSchedule Summarize(
        LoanAmortizationType system,
        decimal principal,
        decimal monthlyRate,
        int termMonths,
        IReadOnlyList<LoanScheduleRow> rows,
        decimal totalCost,
        decimal totalInterest)
    {
        return new LoanSchedule(
            system,
            principal,
            monthlyRate,
            termMonths,
            rows[0].TotalAmount,
            rows[^1].TotalAmount,
            RoundMoney(totalCost / termMonths),
            RoundMoney(totalCost),
            RoundMoney(totalInterest),
            rows);
    }
}
