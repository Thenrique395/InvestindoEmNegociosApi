using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class UploadB3ReportRequestValidator : AbstractValidator<UploadB3ReportRequest>
{
    public UploadB3ReportRequestValidator()
    {
        RuleFor(x => x.File)
            .NotNull().WithMessage("Arquivo PDF é obrigatório.");

        RuleFor(x => x.File!.Length)
            .GreaterThan(0).WithMessage("Arquivo PDF está vazio.")
            .When(x => x.File is not null);
    }
}
