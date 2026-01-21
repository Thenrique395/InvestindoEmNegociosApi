using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.DTOs;

public record CreatePlanRequest(
    MoneyType Type,
    string Title,
    decimal Amount,
    ScheduleType Schedule,
    DateOnly StartDate,
    FrequencyType? Frequency = null,
    int? InstallmentsCount = null,
    int? DefaultPaymentMethodId = null,
    Guid? CategoryId = null,
    Guid? CardId = null
);
