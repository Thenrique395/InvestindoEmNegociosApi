using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class AccountTransferRequestValidator : AbstractValidator<AccountTransferRequest>
{
    public AccountTransferRequestValidator()
    {
        RuleFor(x => x.FromAccountId)
            .NotEmpty().WithMessage("Conta de origem é obrigatória.");

        RuleFor(x => x.ToAccountId)
            .NotEmpty().WithMessage("Conta de destino é obrigatória.");

        RuleFor(x => x.Amount)
            .GreaterThan(0m).WithMessage("Valor da transferência deve ser maior que zero.");

        RuleFor(x => x)
            .Must(x => x.FromAccountId != x.ToAccountId)
            .WithMessage("Conta de origem e destino devem ser diferentes.");

        RuleFor(x => x.Description)
            .MaximumLength(200).WithMessage("Descrição deve ter no máximo 200 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}
