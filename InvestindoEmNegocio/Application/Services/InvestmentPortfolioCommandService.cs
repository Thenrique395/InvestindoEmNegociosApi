using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace InvestindoEmNegocio.Application.Services;

public sealed class InvestmentPortfolioCommandService(
    IInvestmentsService investmentsService,
    IAuditService auditService) : IInvestmentPortfolioCommandService
{
    public async Task<InvestmentAllocationTargetDto> UpsertAllocationTargetAsync(
        Guid userId,
        UpsertInvestmentAllocationTargetRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await investmentsService.UpsertAllocationTargetAsync(userId, request, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            throw new AppProblemException("Alocação inválida", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<InvestmentPositionDto> CreatePositionAsync(
        Guid userId,
        CreateInvestmentPositionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await investmentsService.CreatePositionAsync(userId, request, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            throw new AppProblemException("Posição inválida", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<InvestmentPositionDto?> UpdatePositionAsync(
        Guid userId,
        Guid positionId,
        CreateInvestmentPositionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await investmentsService.UpdatePositionAsync(userId, positionId, request, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            throw new AppProblemException("Posição inválida", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task DeletePositionAsync(
        Guid userId,
        Guid positionId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var removed = await investmentsService.DeletePositionAsync(userId, positionId, cancellationToken);
        if (!removed)
        {
            throw new AppProblemException("Posição não encontrada", "Posição não encontrada.", StatusCodes.Status404NotFound);
        }

        await auditService.LogAsync(
            userId,
            "DELETE",
            "InvestmentPosition",
            positionId.ToString(),
            ipAddress,
            userAgent,
            null,
            cancellationToken);
    }

    public async Task<InvestmentMovementDto> AddMovementAsync(
        Guid userId,
        Guid positionId,
        CreateInvestmentMovementRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await investmentsService.AddMovementAsync(userId, positionId, request, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            throw new AppProblemException("Movimentação inválida", ex.Message, StatusCodes.Status400BadRequest);
        }
    }
}
