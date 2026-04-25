using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using UglyToad.PdfPig.Core;

namespace InvestindoEmNegocio.Application.Services;

public sealed class InvestmentMarketIntegrationService(
    IMarketDataService marketDataService,
    IB3ImportService b3ImportService,
    IB3SyncService b3SyncService,
    ILogger<InvestmentMarketIntegrationService> logger) : IInvestmentMarketIntegrationService
{
    public async Task<MarketQuoteResponse> GetMarketQuoteAsync(string symbol, CancellationToken cancellationToken = default)
    {
        ValidateSymbol(symbol);
        try
        {
            return await marketDataService.GetQuoteAsync(symbol, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao buscar cotação de mercado para {Symbol}", symbol);
            throw new AppProblemException("Dados de mercado indisponíveis", "Não foi possível consultar a cotação neste momento.", StatusCodes.Status503ServiceUnavailable);
        }
    }

    public async Task<MarketProfileResponse> GetMarketProfileAsync(string symbol, CancellationToken cancellationToken = default)
    {
        ValidateSymbol(symbol);
        try
        {
            return await marketDataService.GetProfileAsync(symbol, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao buscar perfil de mercado para {Symbol}", symbol);
            throw new AppProblemException("Dados de mercado indisponíveis", "Não foi possível consultar o perfil do ativo neste momento.", StatusCodes.Status503ServiceUnavailable);
        }
    }

    public async Task<MarketHistoryResponse> GetMarketHistoryAsync(string symbol, string period, CancellationToken cancellationToken = default)
    {
        ValidateSymbol(symbol);
        try
        {
            return await marketDataService.GetHistoryAsync(symbol, period, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao buscar histórico de mercado para {Symbol}", symbol);
            throw new AppProblemException("Dados de mercado indisponíveis", "Não foi possível consultar o histórico do ativo neste momento.", StatusCodes.Status503ServiceUnavailable);
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
            throw new AppProblemException("Erro interno do servidor.", "Não foi possível ler o relatório da B3.", StatusCodes.Status500InternalServerError);
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
            throw new AppProblemException("Erro interno do servidor.", "Não foi possível concluir a importação da B3.", StatusCodes.Status500InternalServerError);
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
            throw new AppProblemException("Erro interno do servidor.", "Não foi possível sincronizar os dados da B3.", StatusCodes.Status500InternalServerError);
        }
    }

    private static void ValidateSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new AppProblemException("Símbolo obrigatório", "Informe o símbolo, por exemplo VALE3.", StatusCodes.Status400BadRequest);
        }
    }
}
