using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class PaymentReversalRequestValidator : AbstractValidator<PaymentReversalRequest>
{
    public PaymentReversalRequestValidator()
    {
        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Observação deve ter no máximo 500 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Note));
    }
}
