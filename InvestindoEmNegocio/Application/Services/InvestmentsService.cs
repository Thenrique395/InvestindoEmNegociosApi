using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

public class InvestmentsService : IInvestmentsService
{
    private readonly IInvestmentGoalRepository _goalRepository;
    private readonly IInvestmentPositionRepository _positionRepository;
    private readonly ILogger<InvestmentsService> _logger;

    public InvestmentsService(IInvestmentGoalRepository goalRepository,
        IInvestmentPositionRepository positionRepository,
        ILogger<InvestmentsService> logger)
    {
        _goalRepository = goalRepository;
        _positionRepository = positionRepository;
        _logger = logger;
    }

    public async Task<InvestmentGoalDto?> GetGoalAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var goal = await _goalRepository.GetByUserAsync(userId, cancellationToken);
        return goal is null ? null : new InvestmentGoalDto(goal.Id, goal.TargetAmount);
    }

    public async Task<InvestmentGoalDto> UpsertGoalAsync(Guid userId, UpsertInvestmentGoalRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _goalRepository.GetByUserAsync(userId, cancellationToken);
        if (existing is null)
        {
            var goal = new InvestmentGoal(userId, request.TargetAmount);
            await _goalRepository.AddAsync(goal, cancellationToken);
            await _goalRepository.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Investment goal created {UserId} {GoalId}", userId, goal.Id);
            return new InvestmentGoalDto(goal.Id, goal.TargetAmount);
        }

        existing.SetTargetAmount(request.TargetAmount);
        await _goalRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Investment goal updated {UserId} {GoalId}", userId, existing.Id);
        return new InvestmentGoalDto(existing.Id, existing.TargetAmount);
    }

    public async Task<List<InvestmentPositionDto>> ListPositionsAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        var positions = await _positionRepository.ListByUserAsync(userId, cancellationToken);
        return positions.Select(Map).ToList();
    }

    public async Task<InvestmentPositionDto?> GetPositionAsync(Guid userId, Guid id,
        CancellationToken cancellationToken = default)
    {
        var position = await _positionRepository.GetByIdAsync(id, userId, cancellationToken);
        return position is null ? null : Map(position);
    }

    public async Task<InvestmentPositionDto> CreatePositionAsync(Guid userId, CreateInvestmentPositionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidatePosition(request);
        var position = new InvestmentPosition(
            userId,
            request.Type,
            request.Asset,
            request.Quantity,
            request.AvgPrice,
            request.OpenedAt,
            request.Account,
            request.Category,
            request.Note);

        await _positionRepository.AddAsync(position, cancellationToken);
        await _positionRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Investment position created {UserId} {PositionId}", userId, position.Id);
        return Map(position);
    }

    public async Task<InvestmentPositionDto?> UpdatePositionAsync(Guid userId, Guid id,
        CreateInvestmentPositionRequest request, CancellationToken cancellationToken = default)
    {
        ValidatePosition(request);
        var position = await _positionRepository.GetByIdAsync(id, userId, cancellationToken);
        if (position is null) return null;

        position.Update(
            request.Type,
            request.Asset,
            request.Quantity,
            request.AvgPrice,
            request.OpenedAt,
            request.Account,
            request.Category,
            request.Note);

        await _positionRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Investment position updated {UserId} {PositionId}", userId, position.Id);
        return Map(position);
    }

    public async Task<bool> DeletePositionAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var position = await _positionRepository.GetByIdAsync(id, userId, cancellationToken);
        if (position is null) return false;
        _positionRepository.Remove(position);
        await _positionRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Investment position deleted {UserId} {PositionId}", userId, position.Id);
        return true;
    }

    public async Task<InvestmentMovementDto> AddMovementAsync(Guid userId, Guid positionId,
        CreateInvestmentMovementRequest request, CancellationToken cancellationToken = default)
    {
        var position = await _positionRepository.GetByIdAsync(positionId, userId, cancellationToken)
                       ?? throw new ArgumentException("Posição não encontrada.");

        if (request.Quantity <= 0 || request.Price <= 0)
            throw new ArgumentException("Quantidade e preço devem ser maiores que zero.");

        if (request.Type == InvestmentMovementType.RESGATE && request.Quantity > position.Quantity)
            throw new ArgumentException("Quantidade de resgate maior que posição.");

        var movement = new InvestmentMovement(position.Id, request.Type, request.Quantity, request.Price, request.Date,
            request.Note);

        var quantity = position.Quantity;
        var avgPrice = position.AvgPrice;

        if (request.Type == InvestmentMovementType.APORTE)
        {
            var totalAtual = quantity * avgPrice;
            var totalNovo = request.Quantity * request.Price;
            quantity = quantity + request.Quantity;
            avgPrice = quantity > 0 ? (totalAtual + totalNovo) / quantity : 0;
        }
        else
            quantity = quantity - request.Quantity;


        position.Update(position.Type, position.Asset, quantity, avgPrice, position.OpenedAt, position.Account,
            position.Category, position.Note);
        position.ApplyMovement(movement);

        await _positionRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Investment movement added {UserId} {PositionId} {MovementId} {Type}", userId, position.Id, movement.Id, movement.Type);
        return Map(movement);
    }

    private static InvestmentPositionDto Map(InvestmentPosition position)
    {
        var movements = position.Movements
            .OrderByDescending(m => m.Date)
            .Select(Map)
            .ToList();

        return new InvestmentPositionDto(
            position.Id,
            position.Type,
            position.Asset,
            position.Quantity,
            position.AvgPrice,
            position.OpenedAt,
            position.Account,
            position.Category,
            position.Note,
            movements);
    }

    private static InvestmentMovementDto Map(InvestmentMovement movement)
    {
        return new InvestmentMovementDto(
            movement.Id,
            movement.Type,
            movement.Quantity,
            movement.Price,
            movement.Date,
            movement.Note);
    }

    private static void ValidatePosition(CreateInvestmentPositionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Asset))
            throw new ArgumentException("Informe o ativo.");

        if (request.Quantity <= 0 || request.AvgPrice <= 0)
            throw new ArgumentException("Quantidade e preço médio devem ser maiores que zero.");
    }
}
