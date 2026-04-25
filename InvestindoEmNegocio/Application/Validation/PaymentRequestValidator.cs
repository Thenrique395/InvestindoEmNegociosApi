using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class PaymentRequestValidator : AbstractValidator<PaymentRequest>
{
    public PaymentRequestValidator()
    {
        RuleFor(x => x.PaidAmount)
            .GreaterThan(0m).WithMessage("Valor pago deve ser maior que zero.");

        RuleFor(x => x.MethodId)
            .GreaterThan(0).WithMessage("Forma de pagamento inválida.")
            .When(x => x.MethodId.HasValue);

        RuleFor(x => x.AccountId)
            .NotEmpty().WithMessage("Conta inválida.")
            .When(x => x.AccountId.HasValue);

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Observação deve ter no máximo 500 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Note));
    }
}
