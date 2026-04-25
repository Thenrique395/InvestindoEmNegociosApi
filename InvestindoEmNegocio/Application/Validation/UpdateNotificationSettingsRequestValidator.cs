using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class UpdateNotificationSettingsRequestValidator : AbstractValidator<UpdateNotificationSettingsRequest>
{
    public UpdateNotificationSettingsRequestValidator()
    {
        RuleFor(x => x.IncomeDaysBefore)
            .GreaterThanOrEqualTo(0).WithMessage("Dias de antecedência para receitas não pode ser negativo.");

        RuleFor(x => x.ExpenseDaysBefore)
            .GreaterThanOrEqualTo(0).WithMessage("Dias de antecedência para despesas não pode ser negativo.");

        RuleFor(x => x.CardCloseDaysBefore)
            .GreaterThanOrEqualTo(0).WithMessage("Dias de antecedência para fechamento do cartão não pode ser negativo.");

        RuleFor(x => x.GoalInactivityDays)
            .GreaterThanOrEqualTo(0).WithMessage("Dias de inatividade da meta não pode ser negativo.");
    }
}
