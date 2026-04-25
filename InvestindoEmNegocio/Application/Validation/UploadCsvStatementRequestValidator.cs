using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class UploadCsvStatementRequestValidator : AbstractValidator<UploadCsvStatementRequest>
{
    public UploadCsvStatementRequestValidator()
    {
        RuleFor(x => x.File)
            .NotNull().WithMessage("Arquivo CSV é obrigatório.");

        RuleFor(x => x.File!.Length)
            .GreaterThan(0).WithMessage("Arquivo CSV está vazio.")
            .When(x => x.File is not null);

        RuleFor(x => x.AccountId)
            .NotNull().WithMessage("Conta é obrigatória.")
            .NotEqual(Guid.Empty).WithMessage("Conta é obrigatória.")
            .When(x => x.AccountId.HasValue);
    }
}
