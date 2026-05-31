using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class UpdateOnboardingRequestValidator : AbstractValidator<UpdateOnboardingRequest>
{
    public UpdateOnboardingRequestValidator()
    {
        RuleFor(x => x.Step)
            .InclusiveBetween(0, 3).WithMessage("Etapa do onboarding deve estar entre 0 e 3.");
    }
}
