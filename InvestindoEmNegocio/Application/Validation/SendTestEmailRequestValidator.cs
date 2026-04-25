using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class SendTestEmailRequestValidator : AbstractValidator<SendTestEmailRequest>
{
    public SendTestEmailRequestValidator()
    {
        RuleFor(x => x.To)
            .NotEmpty().WithMessage("E-mail de destino é obrigatório.")
            .EmailAddress().WithMessage("E-mail de destino é inválido.");
    }
}
