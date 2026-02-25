namespace InvestindoEmNegocio.Application.DTOs;

public record UpdateUserRoleRequest(string Role);

public record UserSummaryResponse(Guid Id, string Name, string Email, string Role, bool IsActive, DateTime CreatedAt);

public record UpdateUserStatusRequest(bool IsActive);

public record SetUserFeatureOverrideRequest(bool IsEnabled);

public record UserFeatureAccessResponse(
    string FeatureKey,
    bool EffectiveEnabled,
    bool EnabledByRole,
    bool? OverrideEnabled);
