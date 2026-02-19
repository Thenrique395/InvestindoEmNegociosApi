using InvestindoEmNegocio.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace InvestindoEmNegocio.Application.DTOs;

public sealed class DataPortabilityOptions
{
    public bool Enabled { get; set; } = false;
    public int MaxImportSizeMb { get; set; } = 20;
    public int ExportCacheSeconds { get; set; } = 30;
}

public sealed class ImportUserDataRequest
{
    public IFormFile File { get; set; } = default!;
    public bool ReplaceExisting { get; set; } = true;
}

public sealed record ImportUserDataResult(int ImportedRecords);

public sealed class UserDataSnapshot
{
    public string Version { get; init; } = "1.1";
    public DateTime ExportedAtUtc { get; init; } = DateTime.UtcNow;
    public Guid SourceUserId { get; init; }
    public UserProfileData? Profile { get; init; }
    public List<CategoryData> Categories { get; init; } = [];
    public List<CardData> Cards { get; init; } = [];
    public List<GoalData> Goals { get; init; } = [];
    public List<GoalContributionData> GoalContributions { get; init; } = [];
    public List<MoneyPlanData> Plans { get; init; } = [];
    public List<MoneyInstallmentData> Installments { get; init; } = [];
    public List<MoneyPaymentData> Payments { get; init; } = [];
    public InvestmentGoalData? InvestmentGoal { get; init; }
    public InvestmentAllocationTargetData? InvestmentAllocationTarget { get; init; }
    public List<InvestmentPositionData> InvestmentPositions { get; init; } = [];
    public List<InvestmentMovementData> InvestmentMovements { get; init; } = [];
    public UserOnboardingData? Onboarding { get; init; }
    public List<UserNotificationData> Notifications { get; init; } = [];
}

public sealed record UserProfileData(
    Guid Id,
    string FullName,
    string Document,
    string Phone,
    DateTime? BirthDate,
    string AvatarUrl,
    string City,
    string State,
    string Country,
    string Language,
    string Currency,
    bool NotifyUpcomingEnabled,
    bool NotifyOverdueEnabled,
    bool NotifyEmailEnabled,
    bool NotifyInAppEnabled,
    int NotifyDaysBeforeDue,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public sealed record CategoryData(Guid Id, string Name, MoneyType? AppliesTo, bool IsActive, DateTime CreatedAt);

public sealed record CardData(
    Guid Id,
    int BrandId,
    string? Bank,
    decimal CreditLimit,
    int StatementCloseDay,
    int DueDay,
    string HolderName,
    string Nickname,
    string Last4,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public sealed record GoalData(
    Guid Id,
    string Title,
    decimal TargetAmount,
    decimal CurrentAmount,
    int Year,
    decimal ExpectedMonthly,
    DateOnly? TargetDate,
    string? Description,
    GoalStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public sealed record GoalContributionData(Guid Id, Guid GoalId, decimal Amount, DateOnly Date, string? Note, DateTime CreatedAt);

public sealed record MoneyPlanData(
    Guid Id,
    MoneyType Type,
    string Title,
    Guid? CategoryId,
    Guid? CardId,
    decimal Amount,
    ScheduleType Schedule,
    FrequencyType? Frequency,
    int? InstallmentsCount,
    int? DefaultPaymentMethodId,
    DateOnly StartDate,
    PlanStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public sealed record MoneyInstallmentData(
    Guid Id,
    Guid PlanId,
    int InstallmentNo,
    DateOnly DueDate,
    DateOnly? OriginalDueDate,
    decimal Amount,
    InstallmentStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public sealed record MoneyPaymentData(
    Guid Id,
    Guid InstallmentId,
    DateTime PaidAt,
    decimal PaidAmount,
    int? MethodId,
    string? Note,
    DateTime CreatedAt
);

public sealed record InvestmentGoalData(Guid Id, decimal TargetAmount, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record InvestmentAllocationTargetData(
    Guid Id,
    decimal Rf,
    decimal Acoes,
    decimal Fundos,
    decimal Cripto,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record InvestmentPositionData(
    Guid Id,
    InvestmentType Type,
    string Asset,
    decimal Quantity,
    decimal AvgPrice,
    DateOnly OpenedAt,
    string Account,
    string Category,
    string? Note,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public sealed record InvestmentMovementData(
    Guid Id,
    Guid PositionId,
    InvestmentMovementType Type,
    decimal Quantity,
    decimal Price,
    DateOnly Date,
    string? Note,
    DateTime CreatedAt
);

public sealed record UserOnboardingData(Guid Id, int Step, bool Completed, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record UserNotificationData(
    Guid Id,
    Guid? PlanId,
    Guid? InstallmentId,
    MoneyType? MoneyType,
    NotificationKind Kind,
    string ReferenceKey,
    string Title,
    string Message,
    DateOnly? DueDate,
    DateTime CreatedAt,
    DateTime? ReadAt
);
