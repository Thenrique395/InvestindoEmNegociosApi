using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Validation;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Tests;

public class PlanAndInstallmentLimitsValidatorTests
{
    private readonly CreatePlanRequestValidator _plan = new();
    private readonly UpdateInstallmentRequestValidator _installment = new();

    private static CreatePlanRequest OneTime(decimal amount) => new(
        MoneyType.Expense, "Teste", amount, ScheduleType.OneTime, new DateOnly(2026, 8, 5));

    private static CreatePlanRequest Installments(int count) => new(
        MoneyType.Expense, "Teste", 100m, ScheduleType.Installments, new DateOnly(2026, 8, 5),
        InstallmentsCount: count);

    // ---- #5: teto de valor no plano ----
    [Fact]
    public void Plan_Amount_Above_Max_Should_Fail()
    {
        _plan.Validate(OneTime(MoneyLimits.MaxAmount + 1m)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Plan_Amount_At_Max_Should_Pass()
    {
        _plan.Validate(OneTime(MoneyLimits.MaxAmount)).IsValid.Should().BeTrue();
    }

    // ---- #3: teto de parcelas ----
    [Fact]
    public void Plan_Installments_Above_Max_Should_Fail()
    {
        _plan.Validate(Installments(MoneyLimits.MaxInstallments + 1)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Plan_Installments_At_Max_Should_Pass()
    {
        _plan.Validate(Installments(MoneyLimits.MaxInstallments)).IsValid.Should().BeTrue();
    }

    // ---- #4 + #5: edição de parcela ----
    [Fact]
    public void Installment_Amount_Zero_Or_Negative_Should_Fail()
    {
        _installment.Validate(new UpdateInstallmentRequest(0m, new DateOnly(2026, 8, 5))).IsValid.Should().BeFalse();
        _installment.Validate(new UpdateInstallmentRequest(-10m, new DateOnly(2026, 8, 5))).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Installment_Amount_Above_Max_Should_Fail()
    {
        _installment.Validate(new UpdateInstallmentRequest(MoneyLimits.MaxAmount + 1m, new DateOnly(2026, 8, 5)))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Installment_Valid_Amount_Should_Pass()
    {
        _installment.Validate(new UpdateInstallmentRequest(1500m, new DateOnly(2026, 8, 5))).IsValid.Should().BeTrue();
    }
}
