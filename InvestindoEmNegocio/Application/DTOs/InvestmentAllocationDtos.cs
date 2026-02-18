namespace InvestindoEmNegocio.Application.DTOs;

public record InvestmentAllocationTargetDto(
    decimal Rf,
    decimal Acoes,
    decimal Fundos,
    decimal Cripto,
    decimal Total);

public record UpsertInvestmentAllocationTargetRequest(
    decimal Rf,
    decimal Acoes,
    decimal Fundos,
    decimal Cripto);
