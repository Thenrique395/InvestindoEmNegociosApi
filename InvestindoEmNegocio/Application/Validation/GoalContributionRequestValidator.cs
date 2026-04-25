using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class GoalContributionRequestValidator : AbstractValidator<GoalContributionRequest>
{
    public GoalContributionRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0m).WithMessage("Valor deve ser maior que zero.");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Observação deve ter no máximo 500 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Note));
    }
}
