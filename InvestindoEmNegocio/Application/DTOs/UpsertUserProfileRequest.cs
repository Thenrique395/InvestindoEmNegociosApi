namespace InvestindoEmNegocio.Application.DTOs;

public record UpsertUserProfileRequest(
    string FullName,
    string Phone,
    DateTime? BirthDate,
    string AvatarUrl,
    string City,
    string State,
    string Country,
    string FinancialGoal,
    string Language,
    int CarryOverDay = 1,
    string IntelligenceMode = "B");
