using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.DTOs;

public record CategoryResponse(Guid Id, string Name, MoneyType? AppliesTo, bool IsDefault, bool IsActive);

public record UpsertCategoryRequest(string Name, MoneyType? AppliesTo);

public record UpdateCategoryStatusRequest(bool IsActive);

/// <summary>
/// Resultado da exclusão de uma categoria de usuário. Categorias em uso por
/// lançamentos são desativadas (preservam o histórico); as demais são removidas.
/// </summary>
public enum CategoryDeletionOutcome
{
    NotFound,
    Deleted,
    Deactivated
}
