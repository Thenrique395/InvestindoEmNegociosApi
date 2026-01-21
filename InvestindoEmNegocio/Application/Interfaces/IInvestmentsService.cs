using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IInvestmentsService
{
    Task<InvestmentGoalDto?> GetGoalAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<InvestmentGoalDto> UpsertGoalAsync(Guid userId, UpsertInvestmentGoalRequest request, CancellationToken cancellationToken = default);
    Task<List<InvestmentPositionDto>> ListPositionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<InvestmentPositionDto?> GetPositionAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<InvestmentPositionDto> CreatePositionAsync(Guid userId, CreateInvestmentPositionRequest request, CancellationToken cancellationToken = default);
    Task<InvestmentPositionDto?> UpdatePositionAsync(Guid userId, Guid id, CreateInvestmentPositionRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeletePositionAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<InvestmentMovementDto> AddMovementAsync(Guid userId, Guid positionId, CreateInvestmentMovementRequest request, CancellationToken cancellationToken = default);
}
