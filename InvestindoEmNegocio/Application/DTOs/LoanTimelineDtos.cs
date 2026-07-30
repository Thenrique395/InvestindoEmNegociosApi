namespace InvestindoEmNegocio.Application.DTOs;

/// <summary>Evento da linha do tempo de um contrato de empréstimo (ordenado do mais recente).</summary>
public sealed record LoanTimelineEvent(
    DateTime At,
    string Type,
    string Title,
    decimal? Amount);
