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

        RuleFor(x => x.Document)
            .NotEmpty().WithMessage("CPF é obrigatório.")
            .Must(BeDigits11).WithMessage("CPF deve ter exatamente 11 dígitos.");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Telefone é obrigatório.")
            .Matches(@"^(\(\d{2}\)\s\d{5}-\d{4}|\+\d{2}\s\d{2}\s\d{9})$").WithMessage("Telefone deve estar no formato (81) 99525-7823.");

        RuleFor(x => x.FinancialGoal)
            .MaximumLength(80).WithMessage("Objetivo financeiro deve ter no máximo 80 caracteres.");
    }

    private bool BeDigits11(string document)
    {
        var digits = new string((document ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length == 11;
    }
}
