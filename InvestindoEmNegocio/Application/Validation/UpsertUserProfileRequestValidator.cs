using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public class UpsertUserProfileRequestValidator : AbstractValidator<UpsertUserProfileRequest>
{
    public UpsertUserProfileRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Nome completo é obrigatório.")
            .MinimumLength(3).WithMessage("Nome completo deve ter ao menos 3 caracteres.");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Telefone é obrigatório.")
            .Matches(@"^(\(\d{2}\)\s\d{5}-\d{4}|\+\d{2}\s\d{2}\s\d{9})$").WithMessage("Telefone deve estar no formato (81) 99525-7823.");

        RuleFor(x => x.FinancialGoal)
            .MaximumLength(80).WithMessage("Objetivo financeiro deve ter no máximo 80 caracteres.");

        RuleFor(x => x.CarryOverDay)
            .InclusiveBetween(1, 31).WithMessage("Dia de competência deve estar entre 1 e 31.");

        RuleFor(x => x.IntelligenceMode)
            .NotEmpty().WithMessage("Modo de inteligência é obrigatório.")
            .Must(mode =>
            {
                var normalized = (mode ?? string.Empty).Trim().ToUpperInvariant();
                return normalized is "B" or "C";
            })
            .WithMessage("Modo de inteligência deve ser B ou C.");
    }
}
