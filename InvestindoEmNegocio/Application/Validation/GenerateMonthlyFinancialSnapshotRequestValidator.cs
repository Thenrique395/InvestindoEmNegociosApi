using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class GenerateMonthlyFinancialSnapshotRequestValidator : AbstractValidator<GenerateMonthlyFinancialSnapshotRequest>
{
    public GenerateMonthlyFinancialSnapshotRequestValidator()
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithMessage("Ano da competência é inválido.");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12).WithMessage("Mês da competência é inválido.");
    }
}
