namespace InvestindoEmNegocio.Application.DTOs;

public record UpdateActiveRequest(bool IsActive);

public record CardBrandAdminResponse(int Id, string Name, string Code, bool IsActive);

public record PaymentMethodAdminResponse(int Id, string Name, bool IsActive);

public record CreateCardBrandRequest(string Name, string Code);

public record CreatePaymentMethodRequest(string Name);

public record InstitutionAdminResponse(int Id, string Name, string Type, bool IsActive);

public record CreateInstitutionRequest(string Name, string Type);

public record AdminCategoryResponse(Guid Id, string Name, string? AppliesTo, bool IsActive, DateTime CreatedAt);

public record AdminCategoryRequest(string Name, string? AppliesTo);
