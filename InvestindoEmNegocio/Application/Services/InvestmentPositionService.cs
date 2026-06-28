using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

public class InvestmentPositionService(
    IInvestmentPositionRepository positionRepository,
    ICurrentSpaceAccessor currentSpaceAccessor,
    IMemoryCache cache,
    ILogger<InvestmentPositionService> logger) :
    IInvestmentPositionQueryService,
    IInvestmentPositionCommandService
{
    private readonly ILogger<InvestmentPositionService> _logger = logger;

    public async Task<List<InvestmentPositionDto>> ListPositionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cacheKey = InvestmentsShared.PositionsCacheKey(userId, currentSpaceAccessor.SpaceId);
        if (cache.TryGetValue(cacheKey, out List<InvestmentPositionDto>? cached) && cached is not null)
            return cached;

        var positions = await positionRepository.ListByUserAsync(userId, cancellationToken);
        var mapped = positions.Select(InvestmentsShared.CreateInvestmentPositionDto).ToList();
        cache.Set(cacheKey, mapped, TimeSpan.FromSeconds(20));
        return mapped;
    }

    public async Task<InvestmentPositionDto?> GetPositionAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var position = await positionRepository.GetByIdAsync(id, userId, cancellationToken);
        return position is null ? null : InvestmentsShared.CreateInvestmentPositionDto(position);
    }

    public async Task<InvestmentPositionDto> CreatePositionAsync(Guid userId, CreateInvestmentPositionRequest request, CancellationToken cancellationToken = default)
    {
        InvestmentsShared.ValidatePosition(request);
        var position = new InvestmentPosition(
            userId,
            currentSpaceAccessor.RequireSpaceId(),
            request.Type,
            request.Asset,
            request.Quantity,
            request.AvgPrice,
            request.OpenedAt,
            request.Account,
            request.Category,
            request.Note,
            request.Currency);

        await positionRepository.AddAsync(position, cancellationToken);
        await positionRepository.SaveChangesAsync(cancellationToken);
        InvalidatePositionsCache(userId);
        _logger.LogInformation("Investment position created {UserId} {PositionId}", userId, position.Id);
        return InvestmentsShared.CreateInvestmentPositionDto(position);
    }

    public async Task<InvestmentPositionDto?> UpdatePositionAsync(Guid userId, Guid id, CreateInvestmentPositionRequest request, CancellationToken cancellationToken = default)
    {
        InvestmentsShared.ValidatePosition(request);
        var position = await positionRepository.GetByIdAsync(id, userId, cancellationToken);
        if (position is null) return null;

        position.Update(request.Type, request.Asset, request.Quantity, request.AvgPrice, request.OpenedAt, request.Account, request.Category, request.Note, request.Currency);
        await positionRepository.SaveChangesAsync(cancellationToken);
        InvalidatePositionsCache(userId);
        _logger.LogInformation("Investment position updated {UserId} {PositionId}", userId, position.Id);
        return InvestmentsShared.CreateInvestmentPositionDto(position);
    }

    public async Task<bool> DeletePositionAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var position = await positionRepository.GetByIdAsync(id, userId, cancellationToken);
        if (position is null) return false;

        var now = DateTime.UtcNow;
        foreach (var movement in position.Movements)
            movement.MarkDeleted(now);

        position.MarkDeleted(now);
        await positionRepository.SaveChangesAsync(cancellationToken);
        InvalidatePositionsCache(userId);
        _logger.LogInformation("Investment position deleted {UserId} {PositionId}", userId, position.Id);
        return true;
    }

    public async Task<InvestmentMovementDto> AddMovementAsync(Guid userId, Guid positionId, CreateInvestmentMovementRequest request, CancellationToken cancellationToken = default)
    {
        var position = await positionRepository.GetByIdAsync(positionId, userId, cancellationToken)
                       ?? throw new AppProblemException("Posição não encontrada", "A posição de investimento especificada não foi encontrada.", StatusCodes.Status404NotFound);

        if (request.Quantity <= 0 || request.Price <= 0)
            throw new ArgumentException("Quantidade e preço devem ser maiores que zero.");

        if (InvestmentsShared.IsOutputMovement(request.Type) && request.Quantity > position.Quantity)
            throw new ArgumentException("Quantidade de resgate maior que posição.");

        var movement = new InvestmentMovement(position.Id, request.Type, request.Quantity, request.Price, request.Date, request.Note);

        var quantity = position.Quantity;
        var avgPrice = position.AvgPrice;

        if (InvestmentsShared.IsInputMovement(request.Type))
        {
            var totalAtual = quantity * avgPrice;
            var totalNovo = request.Quantity * request.Price;
            quantity += request.Quantity;
            avgPrice = quantity > 0 ? (totalAtual + totalNovo) / quantity : 0;
        }
        else if (InvestmentsShared.IsOutputMovement(request.Type))
        {
            quantity -= request.Quantity;
        }

        position.Update(position.Type, position.Asset, quantity, avgPrice, position.OpenedAt, position.Account, position.Category, position.Note, position.Currency);
        position.ApplyMovement(movement);

        await positionRepository.SaveChangesAsync(cancellationToken);
        InvalidatePositionsCache(userId);
        _logger.LogInformation("Investment movement added {UserId} {PositionId} {MovementId} {Type}", userId, position.Id, movement.Id, movement.Type);
        return InvestmentsShared.CreateInvestmentMovementDto(movement);
    }

    private void InvalidatePositionsCache(Guid userId) => cache.Remove(InvestmentsShared.PositionsCacheKey(userId, currentSpaceAccessor.SpaceId));
}
