using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.DTOs;

public record CategoryResponse(Guid Id, string Name, MoneyType? AppliesTo, bool IsDefault);

public record UpsertCategoryRequest(string Name, MoneyType? AppliesTo);
