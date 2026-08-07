using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class UpdateOnboardingRequestValidator : AbstractValidator<UpdateOnboardingRequest>
{
    // Última etapa do onboarding (índice 0..3). Só se pode marcar como concluído nela,
    // evitando pular o onboarding via API (ex.: {step:0, completed:true}).
    private const int LastStep = 3;

    public UpdateOnboardingRequestValidator()
    {
        RuleFor(x => x.Step)
            .InclusiveBetween(0, LastStep).WithMessage("Etapa do onboarding deve estar entre 0 e 3.");

        RuleFor(x => x.Step)
            .Equal(LastStep)
            .When(x => x.Completed)
            .WithMessage("Só é possível concluir o onboarding na última etapa.");
    }
}
