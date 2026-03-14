using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IFinancialAssistantService
{
    Task<FinancialAssistantPromptContextResponse> BuildContextAsync(Guid userId, DateOnly? referenceDate = null, CancellationToken cancellationToken = default);
    Task<FinancialAssistantChatResponse> ChatAsync(Guid userId, FinancialAssistantChatRequest request, CancellationToken cancellationToken = default);
}
