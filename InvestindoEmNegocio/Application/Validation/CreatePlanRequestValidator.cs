using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class CreatePlanRequestValidator : AbstractValidator<CreatePlanRequest>
{
    public CreatePlanRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Título do plano é obrigatório.")
            .MaximumLength(120).WithMessage("Título do plano deve ter no máximo 120 caracteres.");

        RuleFor(x => x.Amount)
            .GreaterThan(0m).WithMessage("Valor do plano deve ser maior que zero.")
            .LessThanOrEqualTo(MoneyLimits.MaxAmount).WithMessage("Valor do plano é alto demais.");

        RuleFor(x => x.DefaultPaymentMethodId)
            .GreaterThan(0).WithMessage("Forma de pagamento padrão inválida.")
            .When(x => x.DefaultPaymentMethodId.HasValue);

        RuleFor(x => x.InstallmentsCount)
            .Equal(1).WithMessage("Plano avulso deve usar 1 parcela.")
            .When(x => x.Schedule == ScheduleType.OneTime && x.InstallmentsCount.HasValue);

        RuleFor(x => x.Frequency)
            .Null().WithMessage("Plano avulso não aceita frequência.")
            .When(x => x.Schedule == ScheduleType.OneTime);

        RuleFor(x => x.InstallmentsCount)
            .NotNull().WithMessage("Plano parcelado requer quantidade de parcelas.")
            .GreaterThanOrEqualTo(2).WithMessage("Plano parcelado requer no mínimo 2 parcelas.")
            .LessThanOrEqualTo(MoneyLimits.MaxInstallments).WithMessage($"Plano parcelado aceita no máximo {MoneyLimits.MaxInstallments} parcelas.")
            .When(x => x.Schedule == ScheduleType.Installments);

        RuleFor(x => x.Frequency)
            .Null().WithMessage("Plano parcelado não aceita frequência.")
            .When(x => x.Schedule == ScheduleType.Installments);

        RuleFor(x => x.Frequency)
            .NotNull().WithMessage("Plano recorrente requer frequência.")
            .When(x => x.Schedule == ScheduleType.Recurring);

        RuleFor(x => x.InstallmentsCount)
            .Null().WithMessage("Plano recorrente não aceita quantidade de parcelas.")
            .When(x => x.Schedule == ScheduleType.Recurring);
    }
}
