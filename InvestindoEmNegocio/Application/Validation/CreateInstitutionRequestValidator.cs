using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class CreateInstitutionRequestValidator : AbstractValidator<CreateInstitutionRequest>
{
    public CreateInstitutionRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nome da instituição é obrigatório.")
            .MaximumLength(120).WithMessage("Nome da instituição deve ter no máximo 120 caracteres.");

        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Tipo da instituição é obrigatório.")
            .Must(type => Enum.TryParse<InstitutionType>(type, true, out _))
            .WithMessage("Tipo da instituição é inválido.");
    }
}
