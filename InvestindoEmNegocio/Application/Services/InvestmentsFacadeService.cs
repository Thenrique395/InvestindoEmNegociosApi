using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using UglyToad.PdfPig.Core;

namespace InvestindoEmNegocio.Application.Services;

public sealed class InvestmentsFacadeService(
    IInvestmentsService investmentsService,
    IMarketDataService marketDataService,
    IAuditService auditService,
    IB3ImportService b3ImportService,
    IB3SyncService b3SyncService,
    ILogger<InvestmentsFacadeService> logger) : IInvestmentsFacadeService
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
            throw new AppProblemException("Alocação alvo inválida", ex.Message, StatusCodes.Status400BadRequest);
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
            throw new AppProblemException("Não encontrado", "Posição não encontrada.", StatusCodes.Status404NotFound);
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
            throw new AppProblemException("Movimento inválido", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<MarketQuoteResponse> GetMarketQuoteAsync(string symbol, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new AppProblemException("Símbolo obrigatório", "Informe o símbolo, ex.: VALE3.", StatusCodes.Status400BadRequest);
        }

        try
        {
            return await marketDataService.GetQuoteAsync(symbol, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao buscar cotação de mercado para {Symbol}", symbol);
            throw new AppProblemException("Market data indisponível", "Não foi possível obter cotação no momento.", StatusCodes.Status503ServiceUnavailable);
        }
    }

    public async Task<MarketProfileResponse> GetMarketProfileAsync(string symbol, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new AppProblemException("Símbolo obrigatório", "Informe o símbolo, ex.: VALE3.", StatusCodes.Status400BadRequest);
        }

        try
        {
            return await marketDataService.GetProfileAsync(symbol, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao buscar perfil de mercado para {Symbol}", symbol);
            throw new AppProblemException("Market data indisponível", "Não foi possível obter perfil do ativo no momento.", StatusCodes.Status503ServiceUnavailable);
        }
    }

    public async Task<MarketHistoryResponse> GetMarketHistoryAsync(string symbol, string period, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new AppProblemException("Símbolo obrigatório", "Informe o símbolo, ex.: VALE3.", StatusCodes.Status400BadRequest);
        }

        try
        {
            return await marketDataService.GetHistoryAsync(symbol, period, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao buscar histórico de mercado para {Symbol}", symbol);
            throw new AppProblemException("Market data indisponível", "Não foi possível obter histórico do ativo no momento.", StatusCodes.Status503ServiceUnavailable);
        }
    }

    public async Task<B3ExtractResponse> ExtractB3Async(Guid userId, Stream pdfStream, CancellationToken cancellationToken = default)
    {
        try
        {
            return await b3ImportService.ExtractAsync(userId, pdfStream, cancellationToken);
        }
        catch (PdfDocumentFormatException ex)
        {
            logger.LogWarning(ex, "Falha ao ler relatorio B3 (PDF inválido).");
            throw new AppProblemException("Falha ao ler PDF", "O arquivo parece inválido ou protegido.", StatusCodes.Status422UnprocessableEntity);
        }
        catch (ArgumentException ex)
        {
            throw new AppProblemException("Relatório inválido", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao extrair relatorio B3.");
            throw new AppProblemException("Erro interno do servidor.", "Nao foi possivel ler o relatório da B3.", StatusCodes.Status500InternalServerError);
        }
    }

    public async Task<B3ConfirmImportResponse> ConfirmB3Async(Guid userId, ConfirmB3ImportRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await b3ImportService.ConfirmAsync(userId, request, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            throw new AppProblemException("Importação inválida", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new AppProblemException("Acesso negado", ex.Message, StatusCodes.Status401Unauthorized);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao confirmar importacao B3.");
            throw new AppProblemException("Erro interno do servidor.", "Nao foi possivel concluir a importação da B3.", StatusCodes.Status500InternalServerError);
        }
    }

    public async Task<B3SyncResponse> SyncB3Async(Guid userId, B3SyncRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await b3SyncService.SyncAsync(userId, request, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            throw new AppProblemException("Sincronização inválida", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao sincronizar B3.");
            throw new AppProblemException("Erro interno do servidor.", "Nao foi possivel sincronizar dados da B3.", StatusCodes.Status500InternalServerError);
        }
    }
}
