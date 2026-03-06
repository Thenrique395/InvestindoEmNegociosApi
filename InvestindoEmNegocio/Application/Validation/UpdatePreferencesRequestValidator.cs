using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;
using System.Linq;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class UpdatePreferencesRequestValidator : AbstractValidator<UpdatePreferencesRequest>
{
    public UpdatePreferencesRequestValidator()
    {
        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Moeda é obrigatória.")
            .Length(3).WithMessage("Moeda deve ter 3 caracteres.")
            .Matches("^[A-Za-z]{3}$").WithMessage("Moeda deve seguir o padrão ISO de 3 letras.");

        RuleFor(x => x.Locales)
            .NotNull().WithMessage("Lista de localizações é obrigatória.")
            .Must(x => x.Count > 0).WithMessage("Informe ao menos uma localização.")
            .Must(x => x.All(loc => !string.IsNullOrWhiteSpace(loc))).WithMessage("As localizações não podem ser vazias.");

        When(x => x.Notifications is not null, () =>
        {
            RuleFor(x => x.Notifications!.DaysBeforeDue)
                .InclusiveBetween(0, 60)
                .WithMessage("Dias antes do vencimento deve estar entre 0 e 60.");
        });
    }
}
