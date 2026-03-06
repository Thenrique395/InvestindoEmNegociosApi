namespace InvestindoEmNegocio.Application.DTOs;

public record UserProfileDto(
    Guid UserId,
    string FullName,
    string Document,
    string Phone,
    DateTime? BirthDate,
    string AvatarUrl,
    string City,
    string State,
    string Country,
    string FinancialGoal,
    int CarryOverDay,
    string IntelligenceMode,
    string Language,
    string Currency,
    List<string> Locales);
