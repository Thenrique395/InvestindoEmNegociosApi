namespace InvestindoEmNegocio.Application.DTOs;

public record ListQuery(int? Page, int? PageSize, string? SortBy, string? SortDir);
