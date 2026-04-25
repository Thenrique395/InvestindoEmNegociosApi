using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class BankStatementImportRequestValidator : AbstractValidator<BankStatementImportRequest>
{
    public BankStatementImportRequestValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty().WithMessage("Conta é obrigatória.");

        RuleFor(x => x.Items)
            .NotNull().WithMessage("Itens do extrato são obrigatórios.")
            .Must(items => items.Count > 0).WithMessage("Informe ao menos um item do extrato.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.PostedAt)
                .NotEmpty().WithMessage("Data da movimentação é obrigatória.");

            item.RuleFor(x => x.Amount)
                .GreaterThan(0m).WithMessage("Valor da movimentação deve ser maior que zero.");

            item.RuleFor(x => x.Description)
                .NotEmpty().WithMessage("Descrição da movimentação é obrigatória.");
        });
    }
}
