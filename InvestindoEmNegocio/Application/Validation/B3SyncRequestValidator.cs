using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class B3SyncRequestValidator : AbstractValidator<B3SyncRequest>
{
    public B3SyncRequestValidator()
    {
        RuleFor(x => x.Strategy)
            .NotEmpty().WithMessage("Estratégia de sincronização é obrigatória.")
            .Must(value => value.Equals("merge", StringComparison.OrdinalIgnoreCase) || value.Equals("replace", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Estratégia de sincronização deve ser merge ou replace.");
    }
}
