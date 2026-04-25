using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class InvoiceImportRequestValidator : AbstractValidator<InvoiceImportRequest>
{
    public InvoiceImportRequestValidator()
    {
        RuleFor(x => x.CardId)
            .NotEqual(Guid.Empty).WithMessage("Cartão inválido.")
            .When(x => x.CardId.HasValue);

        RuleFor(x => x.CategoryId)
            .NotEqual(Guid.Empty).WithMessage("Categoria inválida.")
            .When(x => x.CategoryId.HasValue);

        RuleFor(x => x.Items)
            .NotNull().WithMessage("Itens da fatura são obrigatórios.")
            .Must(items => items.Count > 0).WithMessage("Informe ao menos um item da fatura.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.Description)
                .NotEmpty().WithMessage("Descrição do item é obrigatória.");

            item.RuleFor(x => x.Amount)
                .NotEmpty().WithMessage("Valor do item é obrigatório.");

            item.RuleFor(x => x.BaseDescription)
                .MaximumLength(200).WithMessage("Descrição base deve ter no máximo 200 caracteres.")
                .When(x => !string.IsNullOrWhiteSpace(x.BaseDescription));
        });

        RuleForEach(x => x.Items)
            .SetValidator(new InvoiceImportItemRequestValidator());
    }
}
