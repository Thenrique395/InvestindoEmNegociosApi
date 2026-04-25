using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class UpsertIncomeGoalRequestValidator : AbstractValidator<UpsertIncomeGoalRequest>
{
    public UpsertIncomeGoalRequestValidator()
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithMessage("Ano inválido.");

        RuleFor(x => x.ExpectedMonthly)
            .GreaterThan(0m).WithMessage("Valor mensal deve ser maior que zero.");
    }
}
