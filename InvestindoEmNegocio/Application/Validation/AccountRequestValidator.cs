using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class AccountRequestValidator : AbstractValidator<AccountRequest>
{
    public AccountRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nome da conta é obrigatório.")
            .MaximumLength(100).WithMessage("Nome da conta deve ter no máximo 100 caracteres.");

        RuleFor(x => x.InitialBalance)
            .GreaterThanOrEqualTo(0m).WithMessage("Saldo inicial não pode ser negativo.");
    }
}
