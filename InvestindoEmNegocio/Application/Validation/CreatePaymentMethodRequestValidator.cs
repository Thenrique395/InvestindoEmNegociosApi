using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class CreatePaymentMethodRequestValidator : AbstractValidator<CreatePaymentMethodRequest>
{
    public CreatePaymentMethodRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nome da forma de pagamento é obrigatório.")
            .MaximumLength(80).WithMessage("Nome da forma de pagamento deve ter no máximo 80 caracteres.");
    }
}
