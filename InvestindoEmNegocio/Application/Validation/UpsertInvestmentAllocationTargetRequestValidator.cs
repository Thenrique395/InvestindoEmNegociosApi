using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class UpsertInvestmentAllocationTargetRequestValidator : AbstractValidator<UpsertInvestmentAllocationTargetRequest>
{
    public UpsertInvestmentAllocationTargetRequestValidator()
    {
        RuleFor(x => x.Rf)
            .GreaterThanOrEqualTo(0m).WithMessage("Percentual de renda fixa não pode ser negativo.");

        RuleFor(x => x.Acoes)
            .GreaterThanOrEqualTo(0m).WithMessage("Percentual de ações não pode ser negativo.");

        RuleFor(x => x.Fundos)
            .GreaterThanOrEqualTo(0m).WithMessage("Percentual de fundos não pode ser negativo.");

        RuleFor(x => x.Cripto)
            .GreaterThanOrEqualTo(0m).WithMessage("Percentual de cripto não pode ser negativo.");

        RuleFor(x => x)
            .Must(x => decimal.Round(x.Rf + x.Acoes + x.Fundos + x.Cripto, 2) == 100m)
            .WithMessage("A soma da alocação alvo precisa ser 100%.");
    }
}
