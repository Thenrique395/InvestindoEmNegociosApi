using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class LoanContractRequestValidator : AbstractValidator<LoanContractRequest>
{
    public LoanContractRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Título do empréstimo é obrigatório.")
            .MaximumLength(120).WithMessage("Título do empréstimo deve ter no máximo 120 caracteres.");

        RuleFor(x => x.PrincipalAmount)
            .GreaterThan(0m).WithMessage("Principal deve ser maior que zero.");

        RuleFor(x => x.AnnualInterestRate)
            .GreaterThanOrEqualTo(0m).WithMessage("Taxa de juros inválida.");

        RuleFor(x => x.TermMonths)
            .InclusiveBetween(1, 480).WithMessage("Prazo deve ficar entre 1 e 480 meses.");

        RuleFor(x => x.PaymentDay)
            .InclusiveBetween(1, 28).WithMessage("Dia de pagamento deve ficar entre 1 e 28.");
    }
}
