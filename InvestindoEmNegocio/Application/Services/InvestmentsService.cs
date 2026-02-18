using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

public class InvestmentsService : IInvestmentsService
{
    private const decimal TotalAllocation = 100m;
    private readonly IInvestmentGoalRepository _goalRepository;
    private readonly IInvestmentAllocationTargetRepository _allocationTargetRepository;
    private readonly IInvestmentPositionRepository _positionRepository;
    private readonly ILogger<InvestmentsService> _logger;

    public InvestmentsService(IInvestmentGoalRepository goalRepository,
        IInvestmentAllocationTargetRepository allocationTargetRepository,
        IInvestmentPositionRepository positionRepository,
        ILogger<InvestmentsService> logger)
    {
        _goalRepository = goalRepository;
        _allocationTargetRepository = allocationTargetRepository;
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

    public async Task<InvestmentAllocationTargetDto> GetAllocationTargetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var current = await _allocationTargetRepository.GetByUserAsync(userId, cancellationToken);
        if (current is null)
        {
            return MapAllocation(40m, 35m, 20m, 5m);
        }

        return MapAllocation(current.Rf, current.Acoes, current.Fundos, current.Cripto);
    }

    public async Task<InvestmentAllocationTargetDto> UpsertAllocationTargetAsync(Guid userId, UpsertInvestmentAllocationTargetRequest request, CancellationToken cancellationToken = default)
    {
        ValidateAllocation(request);

        var current = await _allocationTargetRepository.GetByUserAsync(userId, cancellationToken);
        if (current is null)
        {
            var target = new InvestmentAllocationTarget(userId, request.Rf, request.Acoes, request.Fundos, request.Cripto);
            await _allocationTargetRepository.AddAsync(target, cancellationToken);
            await _allocationTargetRepository.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Investment allocation target created {UserId} {TargetId}", userId, target.Id);
            return MapAllocation(target.Rf, target.Acoes, target.Fundos, target.Cripto);
        }

        current.SetAllocation(request.Rf, request.Acoes, request.Fundos, request.Cripto);
        await _allocationTargetRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Investment allocation target updated {UserId} {TargetId}", userId, current.Id);
        return MapAllocation(current.Rf, current.Acoes, current.Fundos, current.Cripto);
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

        if (IsOutputMovement(request.Type) && request.Quantity > position.Quantity)
            throw new ArgumentException("Quantidade de resgate maior que posição.");

        var movement = new InvestmentMovement(position.Id, request.Type, request.Quantity, request.Price, request.Date,
            request.Note);

        var quantity = position.Quantity;
        var avgPrice = position.AvgPrice;

        if (IsInputMovement(request.Type))
        {
            var totalAtual = quantity * avgPrice;
            var totalNovo = request.Quantity * request.Price;
            quantity = quantity + request.Quantity;
            avgPrice = quantity > 0 ? (totalAtual + totalNovo) / quantity : 0;
        }
        else if (IsOutputMovement(request.Type))
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

    private static bool IsInputMovement(InvestmentMovementType type) =>
        type is InvestmentMovementType.APORTE or InvestmentMovementType.COMPRA;

    private static bool IsOutputMovement(InvestmentMovementType type) =>
        type is InvestmentMovementType.RESGATE or InvestmentMovementType.VENDA;

    private static InvestmentAllocationTargetDto MapAllocation(decimal rf, decimal acoes, decimal fundos, decimal cripto)
    {
        var total = rf + acoes + fundos + cripto;
        return new InvestmentAllocationTargetDto(rf, acoes, fundos, cripto, total);
    }

    private static void ValidateAllocation(UpsertInvestmentAllocationTargetRequest request)
    {
        if (request.Rf < 0 || request.Acoes < 0 || request.Fundos < 0 || request.Cripto < 0)
            throw new ArgumentException("Os percentuais não podem ser negativos.");

        var total = request.Rf + request.Acoes + request.Fundos + request.Cripto;
        if (Math.Abs(total - TotalAllocation) > 0.001m)
            throw new ArgumentException("A soma da alocação alvo precisa ser 100%.");
    }
}
