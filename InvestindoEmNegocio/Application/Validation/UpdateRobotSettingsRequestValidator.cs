using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class UpdateRobotSettingsRequestValidator : AbstractValidator<UpdateRobotSettingsRequest>
{
    public UpdateRobotSettingsRequestValidator()
    {
        RuleFor(x => x.DailyRunTimeUtc)
            .NotEmpty().WithMessage("Horário diário é obrigatório.")
            .Matches(@"^\d{2}:\d{2}$").WithMessage("Horário deve estar no formato HH:mm.");
    }
}
