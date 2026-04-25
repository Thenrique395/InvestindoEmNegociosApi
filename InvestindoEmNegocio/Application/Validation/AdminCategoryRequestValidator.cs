using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class AdminCategoryRequestValidator : AbstractValidator<AdminCategoryRequest>
{
    public AdminCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nome da categoria é obrigatório.")
            .MaximumLength(60).WithMessage("Nome da categoria deve ter no máximo 60 caracteres.");

        RuleFor(x => x.AppliesTo)
            .Must(value => string.IsNullOrWhiteSpace(value) || Enum.TryParse<MoneyType>(value, true, out _))
            .WithMessage("Tipo de aplicação é inválido.");
    }
}
