using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class CreateCardBrandRequestValidator : AbstractValidator<CreateCardBrandRequest>
{
    public CreateCardBrandRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nome da bandeira é obrigatório.")
            .MaximumLength(80).WithMessage("Nome da bandeira deve ter no máximo 80 caracteres.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Código da bandeira é obrigatório.")
            .MaximumLength(20).WithMessage("Código da bandeira deve ter no máximo 20 caracteres.")
            .Matches("^[A-Za-z0-9_-]+$").WithMessage("Código da bandeira possui formato inválido.");
    }
}
