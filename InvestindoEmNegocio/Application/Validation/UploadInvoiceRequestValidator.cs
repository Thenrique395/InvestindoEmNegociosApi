using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class UploadInvoiceRequestValidator : AbstractValidator<UploadInvoiceRequest>
{
    public UploadInvoiceRequestValidator()
    {
        RuleFor(x => x.File)
            .NotNull().WithMessage("Arquivo da fatura é obrigatório.");

        RuleFor(x => x.File!.Length)
            .GreaterThan(0).WithMessage("Arquivo da fatura está vazio.")
            .When(x => x.File is not null);
    }
}
