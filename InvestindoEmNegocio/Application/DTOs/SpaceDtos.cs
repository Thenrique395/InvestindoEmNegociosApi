namespace InvestindoEmNegocio.Application.DTOs;

public record SpaceRequest(string Name, string? Password);

public record SpaceResponse(
    Guid Id,
    string Name,
    bool IsDefault,
    bool HasPassword,
    DateTime CreatedAt);

public record EnterSpaceRequest(string? Password);
