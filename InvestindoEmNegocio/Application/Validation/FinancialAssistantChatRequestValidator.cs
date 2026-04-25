using FluentValidation;
using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Validation;

public sealed class FinancialAssistantChatRequestValidator : AbstractValidator<FinancialAssistantChatRequest>
{
    private const int MaxQuestionLength = 600;

    public FinancialAssistantChatRequestValidator()
    {
        RuleFor(x => x.Question)
            .NotEmpty().WithMessage("Pergunta é obrigatória.")
            .MaximumLength(MaxQuestionLength).WithMessage($"Pergunta deve ter no máximo {MaxQuestionLength} caracteres.");
    }
}
