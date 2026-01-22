namespace InvestindoEmNegocio.Application.DTOs;

public record UpdateActiveRequest(bool IsActive);

public record CardBrandAdminResponse(int Id, string Name, string Code, bool IsActive);

public record PaymentMethodAdminResponse(int Id, string Name, bool IsActive);

public record AdminCategoryResponse(Guid Id, string Name, string? AppliesTo, bool IsActive, DateTime CreatedAt);

public record AdminCategoryRequest(string Name, string? AppliesTo);
