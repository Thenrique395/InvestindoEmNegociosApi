using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class CreateGoalRequestValidator : AbstractValidator<CreateGoalRequest>
{
    public CreateGoalRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Título é obrigatório.")
            .MaximumLength(120).WithMessage("Título deve ter no máximo 120 caracteres.");

        RuleFor(x => x.TargetAmount)
            .GreaterThan(0m).WithMessage("Valor da meta deve ser maior que zero.");

        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithMessage("Ano inválido.");

        RuleFor(x => x.CurrentAmount)
            .GreaterThanOrEqualTo(0m).WithMessage("Valor atual não pode ser negativo.");

        RuleFor(x => x.ExpectedMonthly)
            .GreaterThanOrEqualTo(0m).WithMessage("Valor mensal esperado não pode ser negativo.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Descrição deve ter no máximo 500 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}
