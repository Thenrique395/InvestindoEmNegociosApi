using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
namespace InvestindoEmNegocio.Application.Services;

public class InvestmentsService(
    IInvestmentGoalQueryService investmentGoalQueryService,
    IInvestmentGoalCommandService investmentGoalCommandService,
    IInvestmentAllocationQueryService investmentAllocationQueryService,
    IInvestmentAllocationCommandService investmentAllocationCommandService,
    IInvestmentPositionQueryService investmentPositionQueryService,
    IInvestmentPositionCommandService investmentPositionCommandService,
    IInvestmentMarketEnrichmentService investmentMarketEnrichmentService) : IInvestmentsService
{
    public Task<InvestmentGoalDto?> GetGoalAsync(Guid userId, CancellationToken cancellationToken = default) =>
        investmentGoalQueryService.GetGoalAsync(userId, cancellationToken);

    public Task<InvestmentGoalDto> UpsertGoalAsync(Guid userId, UpsertInvestmentGoalRequest request, CancellationToken cancellationToken = default) =>
        investmentGoalCommandService.UpsertGoalAsync(userId, request, cancellationToken);

    public Task<InvestmentAllocationTargetDto> GetAllocationTargetAsync(Guid userId, CancellationToken cancellationToken = default) =>
        investmentAllocationQueryService.GetAllocationTargetAsync(userId, cancellationToken);

    public Task<InvestmentAllocationTargetDto> UpsertAllocationTargetAsync(Guid userId, UpsertInvestmentAllocationTargetRequest request, CancellationToken cancellationToken = default) =>
        investmentAllocationCommandService.UpsertAllocationTargetAsync(userId, request, cancellationToken);

    public Task<List<InvestmentPositionDto>> ListPositionsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        investmentPositionQueryService.ListPositionsAsync(userId, cancellationToken);

    public Task<InvestmentPositionDto?> GetPositionAsync(Guid userId, Guid id, CancellationToken cancellationToken = default) =>
        investmentPositionQueryService.GetPositionAsync(userId, id, cancellationToken);

    public Task<InvestmentPositionDto> CreatePositionAsync(Guid userId, CreateInvestmentPositionRequest request, CancellationToken cancellationToken = default) =>
        investmentPositionCommandService.CreatePositionAsync(userId, request, cancellationToken);

    public Task<InvestmentPositionDto?> UpdatePositionAsync(Guid userId, Guid id, CreateInvestmentPositionRequest request, CancellationToken cancellationToken = default) =>
        investmentPositionCommandService.UpdatePositionAsync(userId, id, request, cancellationToken);

    public Task<bool> DeletePositionAsync(Guid userId, Guid id, CancellationToken cancellationToken = default) =>
        investmentPositionCommandService.DeletePositionAsync(userId, id, cancellationToken);

    public Task<InvestmentMovementDto> AddMovementAsync(Guid userId, Guid positionId, CreateInvestmentMovementRequest request, CancellationToken cancellationToken = default) =>
        investmentPositionCommandService.AddMovementAsync(userId, positionId, request, cancellationToken);

    public Task<List<InvestmentPositionDto>> EnrichWithMarketAsync(List<InvestmentPositionDto> items, CancellationToken cancellationToken = default) =>
        investmentMarketEnrichmentService.EnrichWithMarketAsync(items, cancellationToken);
}
