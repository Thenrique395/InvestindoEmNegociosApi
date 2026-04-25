using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class InvoiceImportItemRequestValidator : AbstractValidator<InvoiceImportItemRequest>
{
    public InvoiceImportItemRequestValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Descrição do item é obrigatória.")
            .MaximumLength(200).WithMessage("Descrição do item deve ter no máximo 200 caracteres.");

        RuleFor(x => x.Amount)
            .NotEmpty().WithMessage("Valor do item é obrigatório.");

        RuleFor(x => x.BaseDescription)
            .MaximumLength(200).WithMessage("Descrição base deve ter no máximo 200 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.BaseDescription));

        RuleFor(x => x.CategoryId)
            .NotEqual(Guid.Empty).WithMessage("Categoria inválida.")
            .When(x => x.CategoryId.HasValue);
    }
}
