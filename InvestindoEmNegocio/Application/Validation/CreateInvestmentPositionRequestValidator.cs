using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class CreateInvestmentPositionRequestValidator : AbstractValidator<CreateInvestmentPositionRequest>
{
    public CreateInvestmentPositionRequestValidator()
    {
        RuleFor(x => x.Asset)
            .NotEmpty().WithMessage("Ativo é obrigatório.")
            .MaximumLength(40).WithMessage("Ativo deve ter no máximo 40 caracteres.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0m).WithMessage("Quantidade deve ser maior que zero.");

        RuleFor(x => x.AvgPrice)
            .GreaterThan(0m).WithMessage("Preço médio deve ser maior que zero.");

        RuleFor(x => x.Account)
            .NotEmpty().WithMessage("Conta é obrigatória.")
            .MaximumLength(80).WithMessage("Conta deve ter no máximo 80 caracteres.");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Categoria é obrigatória.")
            .MaximumLength(80).WithMessage("Categoria deve ter no máximo 80 caracteres.");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Observação deve ter no máximo 500 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Note));
    }
}
