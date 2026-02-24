namespace InvestindoEmNegocio.Application.DTOs;

public sealed record RobotTaskExecutionResult(
    int ItemsGenerated,
    int EmailsAttempted = 0,
    int EmailsSent = 0,
    int EmailsFailed = 0,
    string? ZeroItemsReasonCode = null);
