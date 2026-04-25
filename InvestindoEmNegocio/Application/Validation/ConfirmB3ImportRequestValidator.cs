using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class ConfirmB3ImportRequestValidator : AbstractValidator<ConfirmB3ImportRequest>
{
    public ConfirmB3ImportRequestValidator()
    {
        RuleFor(x => x.ImportToken)
            .NotEmpty().WithMessage("Token de importação é obrigatório.");

        RuleFor(x => x.Strategy)
            .NotEmpty().WithMessage("Estratégia de importação é obrigatória.")
            .Must(value => value.Equals("merge", StringComparison.OrdinalIgnoreCase) || value.Equals("replace", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Estratégia de importação deve ser merge ou replace.");
    }
}
