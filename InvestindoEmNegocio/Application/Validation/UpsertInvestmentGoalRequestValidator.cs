using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class UpsertInvestmentGoalRequestValidator : AbstractValidator<UpsertInvestmentGoalRequest>
{
    public UpsertInvestmentGoalRequestValidator()
    {
        RuleFor(x => x.TargetAmount)
            .GreaterThan(0m).WithMessage("Meta de investimento deve ser maior que zero.");
    }
}
