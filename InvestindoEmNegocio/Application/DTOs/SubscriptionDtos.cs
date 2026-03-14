namespace InvestindoEmNegocio.Application.DTOs;

public sealed record SubscriptionPlanResponse(
    string Code,
    string Name,
    string Role,
    string Description,
    decimal MonthlyPrice,
    decimal YearlyPrice,
    bool Recommended,
    bool Current,
    IReadOnlyList<string> Features,
    IReadOnlyDictionary<string, string> Limits);

public sealed record CurrentSubscriptionResponse(
    string PlanCode,
    string PlanName,
    string Role,
    string Status,
    string BillingCycle,
    decimal PriceAmount,
    string Currency,
    bool AutoRenew,
    DateTime StartedAt,
    DateTime? RenewsAt,
    DateTime? CancelledAt);

public sealed record SubscriptionCatalogResponse(
    CurrentSubscriptionResponse Current,
    IReadOnlyList<SubscriptionPlanResponse> Plans,
    IReadOnlyList<string> Notes);

public sealed record ChangeSubscriptionRequest(string PlanCode, string BillingCycle);

public sealed record SubscriptionChangeResponse(
    CurrentSubscriptionResponse Current,
    AuthResponse Session,
    IReadOnlyList<string> Notes);

public sealed record SecuritySummaryDto(
    int ActiveSessions,
    int FailedLoginAttempts,
    bool IsLocked,
    DateTime? LockoutUntil,
    DateTime? LastLoginAt,
    IReadOnlyList<string> Controls,
    IReadOnlyList<string> Recommendations);

public sealed record RevokeSessionsResponse(int RevokedSessions, DateTime RevokedAtUtc);
