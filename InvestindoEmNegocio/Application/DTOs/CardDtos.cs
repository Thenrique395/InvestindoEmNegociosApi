namespace InvestindoEmNegocio.Application.DTOs;

public record CardRequest(
    int BrandId,
    string HolderName,
    string Last4,
    string? Nickname,
    string? Bank,
    decimal CreditLimit,
    int StatementCloseDay,
    int DueDay);

public record CardResponse(
    Guid Id,
    int BrandId,
    string HolderName,
    string Nickname,
    string Last4,
    string? Bank,
    decimal CreditLimit,
    int StatementCloseDay,
    int DueDay,
    DateTime CreatedAt,
    DateTime UpdatedAt);
