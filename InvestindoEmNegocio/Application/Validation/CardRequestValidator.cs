using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class CardRequestValidator : AbstractValidator<CardRequest>
{
    public CardRequestValidator()
    {
        RuleFor(x => x.BrandId)
            .GreaterThan(0).WithMessage("Bandeira do cartão é obrigatória.");

        RuleFor(x => x.HolderName)
            .NotEmpty().WithMessage("Nome do titular é obrigatório.")
            .MaximumLength(120).WithMessage("Nome do titular deve ter no máximo 120 caracteres.");

        RuleFor(x => x.Last4)
            .NotEmpty().WithMessage("Últimos 4 dígitos são obrigatórios.")
            .Matches(@"^\D*(\d\D*){4}$").WithMessage("Últimos 4 dígitos devem conter exatamente 4 números.");

        RuleFor(x => x.Nickname)
            .MaximumLength(120).WithMessage("Apelido deve ter no máximo 120 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Nickname));

        RuleFor(x => x.Bank)
            .MaximumLength(120).WithMessage("Banco deve ter no máximo 120 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Bank));

        RuleFor(x => x.CreditLimit)
            .GreaterThanOrEqualTo(0m).WithMessage("Limite de crédito não pode ser negativo.");

        RuleFor(x => x.StatementCloseDay)
            .InclusiveBetween(1, 31).WithMessage("Dia de fechamento deve estar entre 1 e 31.");

        RuleFor(x => x.DueDay)
            .InclusiveBetween(1, 31).WithMessage("Dia de vencimento deve estar entre 1 e 31.");
    }
}
