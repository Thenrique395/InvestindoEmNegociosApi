using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface ICategorizationService
{
    Task<CategorizationSuggestionDto?> SuggestAsync(
        Guid userId,
        MoneyType type,
        string description,
        CancellationToken cancellationToken = default);

    Task LearnAsync(
        Guid userId,
        MoneyType type,
        string description,
        Guid categoryId,
        CancellationToken cancellationToken = default);
}
