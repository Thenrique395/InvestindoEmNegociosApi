using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class ChangeSubscriptionRequestValidator : AbstractValidator<ChangeSubscriptionRequest>
{
    private static readonly string[] AllowedBillingCycles = ["Monthly", "Yearly"];

    public ChangeSubscriptionRequestValidator()
    {
        RuleFor(x => x.PlanCode)
            .NotEmpty().WithMessage("Plano é obrigatório.");

        RuleFor(x => x.BillingCycle)
            .NotEmpty().WithMessage("Ciclo de cobrança é obrigatório.")
            .Must(value => AllowedBillingCycles.Contains(value, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Ciclo de cobrança deve ser Monthly ou Yearly.");
    }
}
