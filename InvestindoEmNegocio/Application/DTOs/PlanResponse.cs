using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Application.DTOs;

public record PlanResponse(
    Guid Id,
    MoneyType Type,
    string Title,
    decimal Amount,
    ScheduleType Schedule,
    FrequencyType? Frequency,
    int? InstallmentsCount,
    DateOnly StartDate,
    string Status,
    Guid? CategoryId,
    Guid? CardId,
    // Forma de pagamento escolhida no cadastro. Já era gravada; faltava voltar
    // na leitura, então a listagem só sabia dizer "à vista" ou "cartão".
    int? DefaultPaymentMethodId = null);
