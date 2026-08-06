using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class UpdateInstallmentRequestValidator : AbstractValidator<UpdateInstallmentRequest>
{
    public UpdateInstallmentRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0m).WithMessage("Valor da parcela deve ser maior que zero.")
            .LessThanOrEqualTo(MoneyLimits.MaxAmount).WithMessage("Valor da parcela é alto demais.");
    }
}
