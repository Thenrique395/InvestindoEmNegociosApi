namespace InvestindoEmNegocio.Application.DTOs;

public record UpsertUserProfileRequest(
    string FullName,
    string Document,
    string Phone,
    DateTime? BirthDate,
    string AvatarUrl,
    string City,
    string State,
    string Country,
    string Language);
