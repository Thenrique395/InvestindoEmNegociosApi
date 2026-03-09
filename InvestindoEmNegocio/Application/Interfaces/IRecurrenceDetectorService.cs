using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IRecurrenceDetectorService
{
    Task<RecurrenceSuggestionDto?> SuggestAsync(
        Guid userId,
        MoneyType type,
        string description,
        decimal amount,
        DateTime? occurredAtUtc,
        CancellationToken cancellationToken = default);
}
