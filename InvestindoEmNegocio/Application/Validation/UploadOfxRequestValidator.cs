using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class UploadOfxRequestValidator : AbstractValidator<UploadOfxRequest>
{
    public UploadOfxRequestValidator()
    {
        RuleFor(x => x.File)
            .NotNull().WithMessage("Arquivo OFX é obrigatório.");

        RuleFor(x => x.File!.Length)
            .GreaterThan(0).WithMessage("Arquivo OFX está vazio.")
            .When(x => x.File is not null);

        RuleFor(x => x.AccountId)
            .NotNull().WithMessage("Conta é obrigatória.")
            .NotEqual(Guid.Empty).WithMessage("Conta é obrigatória.")
            .When(x => x.AccountId.HasValue);
    }
}
