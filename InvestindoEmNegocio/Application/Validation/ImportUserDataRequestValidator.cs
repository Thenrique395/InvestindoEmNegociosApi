using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class ImportUserDataRequestValidator : AbstractValidator<ImportUserDataRequest>
{
    public ImportUserDataRequestValidator()
    {
        RuleFor(x => x.File)
            .NotNull().WithMessage("Arquivo JSON é obrigatório.");

        RuleFor(x => x.File.Length)
            .GreaterThan(0).WithMessage("Arquivo JSON está vazio.")
            .When(x => x.File is not null);
    }
}
