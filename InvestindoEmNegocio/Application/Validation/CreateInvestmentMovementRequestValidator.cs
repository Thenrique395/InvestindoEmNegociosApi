using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class CreateInvestmentMovementRequestValidator : AbstractValidator<CreateInvestmentMovementRequest>
{
    public CreateInvestmentMovementRequestValidator()
    {
        RuleFor(x => x.Quantity)
            .GreaterThan(0m).WithMessage("Quantidade deve ser maior que zero.");

        RuleFor(x => x.Price)
            .GreaterThan(0m).WithMessage("Preço deve ser maior que zero.");

        RuleFor(x => x.Note)
            .MaximumLength(400).WithMessage("Observação deve ter no máximo 400 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Note));
    }
}
