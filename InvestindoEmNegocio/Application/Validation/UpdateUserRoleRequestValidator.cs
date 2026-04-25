using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class UpdateUserRoleRequestValidator : AbstractValidator<UpdateUserRoleRequest>
{
    public UpdateUserRoleRequestValidator()
    {
        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Perfil é obrigatório.")
            .Must(role => Enum.TryParse<UserRole>(role, true, out _))
            .WithMessage("Perfil informado é inválido.");
    }
}
