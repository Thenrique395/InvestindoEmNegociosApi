namespace InvestindoEmNegocio.Application.DTOs;

public record UpdateOnboardingRequest(
    int Step,
    bool Completed);
