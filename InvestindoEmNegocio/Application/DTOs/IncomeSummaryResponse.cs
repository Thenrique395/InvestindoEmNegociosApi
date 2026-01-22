using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.DTOs;

public record IncomeSummaryResponse(
    string Month,
    decimal Total,
    decimal TotalRecurring,
    decimal TotalOneTime,
    IReadOnlyList<IncomeItemResponse> Items,
    IncomeMonthSummary? PreviousMonth,
    IReadOnlyList<IncomeMonthSummary> History);

public record IncomeMonthSummary(
    string Month,
    decimal Total,
    decimal TotalRecurring,
    decimal TotalOneTime);

public record IncomeItemResponse(
    Guid Id,
    Guid PlanId,
    string Source,
    decimal Amount,
    string ReceivedOn,
    ScheduleType Schedule,
    string StartDateIso,
    bool IsRecurring,
    string? RecurringStart);
