using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IInvestmentPortfolioCommandService
{
    Task<InvestmentAllocationTargetDto> UpsertAllocationTargetAsync(Guid userId, UpsertInvestmentAllocationTargetRequest request, CancellationToken cancellationToken = default);
    Task<InvestmentPositionDto> CreatePositionAsync(Guid userId, CreateInvestmentPositionRequest request, CancellationToken cancellationToken = default);
    Task<InvestmentPositionDto?> UpdatePositionAsync(Guid userId, Guid positionId, CreateInvestmentPositionRequest request, CancellationToken cancellationToken = default);
    Task DeletePositionAsync(Guid userId, Guid positionId, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);
    Task<InvestmentMovementDto> AddMovementAsync(Guid userId, Guid positionId, CreateInvestmentMovementRequest request, CancellationToken cancellationToken = default);
}
