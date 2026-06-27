using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.Services;

internal static class InvestmentsShared
{
    public const decimal TotalAllocation = 100m;

    public static string PositionsCacheKey(Guid userId) => $"investments:positions:{userId:N}";

    public static InvestmentAllocationTargetDto MapAllocation(decimal rf, decimal acoes, decimal fundos, decimal cripto)
    {
        var total = rf + acoes + fundos + cripto;
        return new InvestmentAllocationTargetDto(rf, acoes, fundos, cripto, total);
    }

    public static InvestmentPositionDto CreateInvestmentPositionDto(InvestmentPosition position)
    {
        var movements = position.Movements
            .OrderByDescending(m => m.Date)
            .Select(CreateInvestmentMovementDto)
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
            movements,
            position.Currency);
    }

    public static InvestmentMovementDto CreateInvestmentMovementDto(InvestmentMovement movement)
    {
        return new InvestmentMovementDto(
            movement.Id,
            movement.Type,
            movement.Quantity,
            movement.Price,
            movement.Date,
            movement.Note);
    }

    public static void ValidatePosition(CreateInvestmentPositionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Asset))
            throw new ArgumentException("Informe o ativo.");

        if (request.Quantity <= 0 || request.AvgPrice <= 0)
            throw new ArgumentException("Quantidade e preço médio devem ser maiores que zero.");
    }

    public static void ValidateAllocation(UpsertInvestmentAllocationTargetRequest request)
    {
        if (request.Rf < 0 || request.Acoes < 0 || request.Fundos < 0 || request.Cripto < 0)
            throw new ArgumentException("Os percentuais não podem ser negativos.");

        var total = request.Rf + request.Acoes + request.Fundos + request.Cripto;
        if (Math.Abs(total - TotalAllocation) > 0.001m)
            throw new ArgumentException("A soma da alocação alvo precisa ser 100%.");
    }

    public static bool IsInputMovement(InvestmentMovementType type) =>
        type is InvestmentMovementType.APORTE or InvestmentMovementType.COMPRA;

    public static bool IsOutputMovement(InvestmentMovementType type) =>
        type is InvestmentMovementType.RESGATE or InvestmentMovementType.VENDA;

    public static string? ExtractTicker(string asset)
    {
        if (string.IsNullOrWhiteSpace(asset)) return null;
        var match = System.Text.RegularExpressions.Regex.Match(asset.ToUpperInvariant(), "[A-Z]{4}[0-9]{1,2}");
        return match.Success ? match.Value : null;
    }
}
