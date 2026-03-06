using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class DeleteOwnAccountRequestValidator : AbstractValidator<DeleteOwnAccountRequest>
{
    public DeleteOwnAccountRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Senha atual é obrigatória.");

        RuleFor(x => x.ConfirmationText)
            .NotEmpty().WithMessage("Texto de confirmação é obrigatório.")
            .MaximumLength(32).WithMessage("Texto de confirmação inválido.");
    }
}
